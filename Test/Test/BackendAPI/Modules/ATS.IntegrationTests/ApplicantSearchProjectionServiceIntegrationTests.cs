using ATS.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class ApplicantSearchProjectionServiceIntegrationTests : BaseIntegrationTest
{
	private static readonly DateTime OrderCreatedAt = new(2026, 8, 1, 8, 30, 0, DateTimeKind.Utc);
	private static readonly DateTime OrderCompletedAt = new(2026, 8, 3, 14, 45, 0, DateTimeKind.Utc);

	public ApplicantSearchProjectionServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task ProcessPendingProjectionsAsync_ShouldCreateProjectionFromCompleteApplicantGraph()
	{
		// Arrange
		var invitation = CreateInvitation("Complete", includeDetails: true);
		await AddInvitationAsync(invitation);
		var startedAt = DateTime.UtcNow;

		// Act
		await _applicantSearchProjectionService.ProcessPendingProjectionsAsync(CancellationToken.None);
		var completedAt = DateTime.UtcNow;

		// Assert
		var projection = await _dbContext.ApplicantSearchProjections
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationRequestId == invitation.EmailInvitationID);

		projection.Should().BeEquivalentTo(new
		{
			invitation.EmailInvitationID,
			FirstName = "Complete First",
			LastName = "Complete Last",
			MiddleInitial = "C",
			EmailAddress = "complete@example.com",
			MobileNumber = "+639171234567",
			PackageId = DefaultPackageId,
			SelectPackage = "Premium Screening",
			RushNormal = "Rush",
			OrderStatus = "Completed",
			OrderCreatedAt,
			OrderCompletedAt,
			ApplicationFormStatus = "Done",
			PositionAppliedFor = "Senior Analyst",
			MaritalStatus = "Single",
			Nationality = "Filipino",
			Sex = "Female",
			DOB = new DateOnly(1994, 5, 12),
			SSS = "12-3456789-0",
			TIN = "123-456-789",
			EmailAlternative = "complete.alt@example.com",
			CurrentAddress = "123 Current Street",
			CurrentCity = "Makati",
			CurrentProvince = "Metro Manila",
			CurrentCountry = "Philippines",
			CurrentPostalCode = "1200",
			PermanentAddress = "456 Permanent Street",
			PermanentCity = "Cebu City",
			PermanentProvince = "Cebu",
			PermanentCountry = "Philippines",
			PermanentPostalCode = "6000",
			HighestEducationalAttainment = "Master's Degree",
			BachelorsSchoolName = "State University",
			BachelorsDegree = "BS Psychology",
			MastersSchoolName = "Graduate University",
			MastersDegree = "MA Psychology",
			PhDSchoolName = "Doctoral University",
			DoctorateDegree = "PhD Psychology",
			LicenseName = "Psychometrician",
			LicenseNumber = "LIC-12345",
			LicenseExpiryDate = new DateOnly(2028, 12, 31),
			Emp1CompanyName = "First Employer",
			Emp1JobTitle = "Analyst",
			Emp2CompanyName = "Second Employer",
			Emp2JobTitle = "Senior Analyst",
			Emp3CompanyName = "Third Employer",
			Emp3JobTitle = "Lead Analyst",
			Ref1FullName = "First Reference",
			Ref1ContactNumber = "09170000001",
			Ref2FullName = "Second Reference",
			Ref2ContactNumber = "09170000002",
			Ref3FullName = "Third Reference",
			Ref3ContactNumber = "09170000003",
			SignerName = "Complete Applicant",
			SignatureDate = new DateOnly(2026, 8, 2)
		}, options => options.ExcludingMissingMembers());

		projection.EmailInvitationRequestId.Should().Be(invitation.EmailInvitationID);
		projection.ProjectionUpdatedAt.Should().BeOnOrAfter(startedAt.AddMilliseconds(-1));
		projection.ProjectionUpdatedAt.Should().BeOnOrBefore(completedAt.AddMilliseconds(1));

		var processedInvitation = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == invitation.EmailInvitationID);
		processedInvitation.NeedsProjection.Should().BeFalse();
		processedInvitation.ProjectionUpdatedAt.Should().NotBeNull();
		processedInvitation.ProjectionUpdatedAt!.Value.Should().BeOnOrAfter(startedAt.AddMilliseconds(-1));
		processedInvitation.ProjectionUpdatedAt.Value.Should().BeOnOrBefore(completedAt.AddMilliseconds(1));
	}

	[Fact]
	public async Task ProcessPendingProjectionsAsync_ShouldRefreshExistingProjectionAndClearMissingDetailValues()
	{
		// Arrange
		var invitation = CreateInvitation("Updated");
		invitation.ApplicantSearchProjection = new ApplicantSearchProjection
		{
			EmailInvitationRequestId = invitation.EmailInvitationID,
			FirstName = "Stale First",
			PositionAppliedFor = "Stale Position",
			CurrentCity = "Stale City",
			ProjectionUpdatedAt = DateTime.UtcNow.AddDays(-1)
		};
		await AddInvitationAsync(invitation);

		// Act
		await _applicantSearchProjectionService.ProcessPendingProjectionsAsync(CancellationToken.None);

		// Assert
		var projections = await _dbContext.ApplicantSearchProjections
			.AsNoTracking()
			.Where(item => item.EmailInvitationRequestId == invitation.EmailInvitationID)
			.ToListAsync();

		projections.Should().ContainSingle();
		projections[0].FirstName.Should().Be("Updated First");
		projections[0].LastName.Should().Be("Updated Last");
		projections[0].PositionAppliedFor.Should().BeNull();
		projections[0].CurrentCity.Should().BeNull();
		projections[0].ProjectionUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task ProcessPendingProjectionsAsync_ShouldProcessOnlyPendingInvitationsAndRemainIdempotent()
	{
		// Arrange
		var firstPending = CreateInvitation("Alpha");
		var secondPending = CreateInvitation("Beta");
		var current = CreateInvitation("Current", needsProjection: false);
		await _dbContext.EmailInvitationRequests.AddRangeAsync(firstPending, secondPending, current);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		// Act
		await _applicantSearchProjectionService.ProcessPendingProjectionsAsync(CancellationToken.None);

		var firstPass = await _dbContext.ApplicantSearchProjections
			.AsNoTracking()
			.OrderBy(item => item.FirstName)
			.ToListAsync();
		var firstPassTimestamps = firstPass.ToDictionary(
			item => item.EmailInvitationRequestId,
			item => item.ProjectionUpdatedAt);

		await _applicantSearchProjectionService.ProcessPendingProjectionsAsync(CancellationToken.None);

		// Assert
		var secondPass = await _dbContext.ApplicantSearchProjections
			.AsNoTracking()
			.OrderBy(item => item.FirstName)
			.ToListAsync();

		secondPass.Should().HaveCount(2);
		secondPass.Select(item => item.EmailInvitationRequestId)
			.Should().BeEquivalentTo([firstPending.EmailInvitationID, secondPending.EmailInvitationID]);
		secondPass.Should().OnlyContain(item =>
			item.ProjectionUpdatedAt == firstPassTimestamps[item.EmailInvitationRequestId]);

		var invitationStates = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.ToDictionaryAsync(item => item.EmailInvitationID);
		invitationStates[firstPending.EmailInvitationID].NeedsProjection.Should().BeFalse();
		invitationStates[secondPending.EmailInvitationID].NeedsProjection.Should().BeFalse();
		invitationStates[current.EmailInvitationID].NeedsProjection.Should().BeFalse();
		invitationStates[current.EmailInvitationID].ProjectionUpdatedAt.Should().BeNull();
	}

	[Fact]
	public async Task ProcessPendingProjectionsAsync_ShouldDoNothing_WhenNoInvitationNeedsProjection()
	{
		// Arrange
		var invitation = CreateInvitation("No Work", needsProjection: false);
		await AddInvitationAsync(invitation);

		// Act
		await _applicantSearchProjectionService.ProcessPendingProjectionsAsync(CancellationToken.None);

		// Assert
		var projectionExists = await _dbContext.ApplicantSearchProjections
			.AsNoTracking()
			.AnyAsync();
		projectionExists.Should().BeFalse();

		var persisted = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync();
		persisted.ProjectionUpdatedAt.Should().BeNull();
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task ProcessPendingProjectionsAsync_ShouldRollback_WhenProjectionViolatesDatabaseConstraints()
	{
		// Arrange
		var invitation = CreateInvitation("Rollback", includeDetails: true);
		invitation.PersonalDetails!.PositionAppliedFor = new string('X', 256);
		await AddInvitationAsync(invitation);

		// Act
		Func<Task> act = () => _applicantSearchProjectionService.ProcessPendingProjectionsAsync(
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<DbUpdateException>();
		_dbContext.ChangeTracker.Clear();

		var projectionExists = await _dbContext.ApplicantSearchProjections
			.AsNoTracking()
			.AnyAsync(item => item.EmailInvitationRequestId == invitation.EmailInvitationID);
		projectionExists.Should().BeFalse();

		var persisted = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == invitation.EmailInvitationID);
		persisted.NeedsProjection.Should().BeTrue();
		persisted.ProjectionUpdatedAt.Should().BeNull();
	}

	#endregion

	private async Task AddInvitationAsync(EmailInvitationRequest invitation)
	{
		await _dbContext.EmailInvitationRequests.AddAsync(invitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();
	}

	private static EmailInvitationRequest CreateInvitation(
		string prefix,
		bool needsProjection = true,
		bool includeDetails = false)
	{
		var id = Guid.CreateVersion7();
		var now = DateTime.UtcNow;
		var invitation = new EmailInvitationRequest
		{
			EmailInvitationID = id,
			FirstName = $"{prefix} First",
			LastName = $"{prefix} Last",
			MiddleInitial = prefix[..1],
			EmailAddress = $"{prefix.Replace(" ", ".").ToLowerInvariant()}@example.com",
			MobileNumber = "+639171234567",
			Requestor = "ATS Integration Tests",
			PackageId = DefaultPackageId,
			SelectPackage = "Premium Screening",
			RushNormal = "Rush",
			HashToken = $"hash-{id}",
			ApplicationFormStatus = "Done",
			EmailSentStatus = "Sent",
			HashTokenCreatedAt = now,
			HashTokenExpiration = now.AddDays(1),
			OrderStatus = "Completed",
			OrderCreatedAt = OrderCreatedAt,
			OrderCompletedAt = OrderCompletedAt,
			NeedsProjection = needsProjection
		};

		if (!includeDetails)
			return invitation;

		invitation.EmailAddress = "complete@example.com";
		invitation.MiddleInitial = "C";
		invitation.PersonalDetails = new PersonalDetails
		{
			PersonalID = id,
			EmailInvitationID = id,
			PositionAppliedFor = "Senior Analyst",
			MaritalStatus = "Single",
			Nationality = "Filipino",
			Sex = "Female",
			DOB = new DateOnly(1994, 5, 12),
			SSS = "12-3456789-0",
			TIN = "123-456-789",
			EmailAlternative = "complete.alt@example.com",
			CreatedDate = now
		};
		invitation.AddressDetails = new AddressDetails
		{
			AddressId = id,
			EmailInvitationID = id,
			CurrentAddress = "123 Current Street",
			CurrentCity = "Makati",
			CurrentProvince = "Metro Manila",
			CurrentCountry = "Philippines",
			CurrentPostalCode = "1200",
			PermanentAddress = "456 Permanent Street",
			PermanentCity = "Cebu City",
			PermanentProvince = "Cebu",
			PermanentCountry = "Philippines",
			PermanentPostalCode = "6000",
			CreatedDate = now
		};
		invitation.EducationalBackground = new EducationalBackground
		{
			EducationalBackgroundID = id,
			EmailInvitationID = id,
			HighestEducationalAttainment = "Master's Degree",
			BachelorsSchoolName = "State University",
			BachelorsDegree = "BS Psychology",
			MastersSchoolName = "Graduate University",
			MastersDegree = "MA Psychology",
			PhDSchoolName = "Doctoral University",
			DoctorateDegree = "PhD Psychology",
			CreatedDate = now
		};
		invitation.LicensesDetails = new LicensesDetails
		{
			LicensesDetailsID = id,
			EmailInvitationID = id,
			LicenseName = "Psychometrician",
			LicenseNumber = "LIC-12345",
			LicenseExpiryDate = new DateOnly(2028, 12, 31),
			CreatedDate = now
		};
		invitation.ProfessionalExperiences = new ProfessionalExperiences
		{
			ProfessionalExperiencesID = id,
			EmailInvitationID = id,
			Emp1CompanyName = "First Employer",
			Emp1JobTitle = "Analyst",
			Emp2CompanyName = "Second Employer",
			Emp2JobTitle = "Senior Analyst",
			Emp3CompanyName = "Third Employer",
			Emp3JobTitle = "Lead Analyst",
			CreatedDate = now
		};
		invitation.ReferenceDetails = new ReferenceDetails
		{
			ReferenceDetailsID = id,
			EmailInvitationID = id,
			Ref1FullName = "First Reference",
			Ref1ContactNumber = "09170000001",
			Ref2FullName = "Second Reference",
			Ref2ContactNumber = "09170000002",
			Ref3FullName = "Third Reference",
			Ref3ContactNumber = "09170000003",
			CreatedDate = now
		};
		invitation.SignatureDetails = new SignatureDetails
		{
			SignatureDetailsID = id,
			EmailInvitationID = id,
			SignerName = "Complete Applicant",
			SignatureDate = new DateOnly(2026, 8, 2)
		};

		return invitation;
	}
}
