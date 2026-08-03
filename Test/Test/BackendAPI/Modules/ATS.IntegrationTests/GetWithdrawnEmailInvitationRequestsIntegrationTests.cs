using ATS.Data.Entities;
using FluentAssertions;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class GetWithdrawnEmailInvitationRequestsIntegrationTests : BaseIntegrationTest
{
	public GetWithdrawnEmailInvitationRequestsIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Positive Path
	[Fact]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldReturnWithdrawnRecords()
	{
		// Arrange
		var withdrawn1 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester1",
			MiddleInitial = "A",
			EmailAddress = "withdrawn1@example.com",
			MobileNumber = "09171234567",
			HashToken = "valid-hash-token-1",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		var withdrawn2 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester2",
			MiddleInitial = "B",
			EmailAddress = "withdrawn2@example.com",
			MobileNumber = "09171234568",
			HashToken = "valid-hash-token-2",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Premium",
			RushNormal = "Rush",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		var active = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Active",
			LastName = "Candidate",
			MiddleInitial = "C",
			EmailAddress = "active@example.com",
			MobileNumber = "09171234569",
			HashToken = "valid-hash-token-3",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info"
		};

		await _dbContext.EmailInvitationRequests.AddRangeAsync(withdrawn1, withdrawn2, active);
		await _dbContext.SaveChangesAsync();

      // Act
		var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.PaginationRequest(1, 10), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Data.Should().HaveCount(2);
		result.Data.Should().AllSatisfy(x => x.OrderStatus.Should().Be("Application Withdrawn"));
		result.Data.Select(x => x.EmailAddress).Should().Contain(new[] { "withdrawn1@example.com", "withdrawn2@example.com" });
	}

	[Fact]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldReturnPaginatedResults()
	{
		// Arrange
		var withdrawnRecords = new List<EmailInvitationRequest>();
		for (int i = 0; i < 15; i++)
		{
			withdrawnRecords.Add(new EmailInvitationRequest
			{
				EmailInvitationID = Guid.CreateVersion7(),
				FirstName = $"Tester{i}",
				LastName = $"Withdrawn{i}",
				MiddleInitial = "T",
				EmailAddress = $"withdrawn{i}@example.com",
				MobileNumber = $"0917123456{i % 10}",
				HashToken = $"hash-token-{i}",
				HashTokenCreatedAt = DateTime.UtcNow,
				HashTokenExpiration = DateTime.UtcNow.AddDays(1),
				SelectPackage = "Standard",
				RushNormal = "Normal",
				EmailSentStatus = "Done",
				ApplicationFormStatus = "Pending",
				OrderStatus = "Application Withdrawn"
			});
		}

		await _dbContext.EmailInvitationRequests.AddRangeAsync(withdrawnRecords);
		await _dbContext.SaveChangesAsync();

     // Act - Get first page
		var page1 = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.PaginationRequest(1, 10), CancellationToken.None);

		// Assert
		page1.Should().NotBeNull();
		page1!.Data.Should().HaveCount(10);
       page1.Count.Should().Be(15);
		page1.PageIndex.Should().Be(1);
		page1.PageSize.Should().Be(10);

        // Act - Get second page
		var page2 = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.PaginationRequest(2, 10), CancellationToken.None);

		// Assert
		page2.Should().NotBeNull();
		page2!.Data.Should().HaveCount(5);
        page2.PageIndex.Should().Be(2);
	}

	[Fact]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldReturnEmptyWhenNoWithdrawnRecords()
	{
		// Arrange
		var active = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Active",
			LastName = "Candidate",
			MiddleInitial = "C",
			EmailAddress = "active@example.com",
			MobileNumber = "09171234569",
			HashToken = "valid-hash-token-3",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(active);
		await _dbContext.SaveChangesAsync();

      // Act
		var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.PaginationRequest(1, 10), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Data.Should().BeEmpty();
        result.Count.Should().Be(0);
	}


	[Fact]
	public async Task SearchWithdrawnEmailInvitationRequests_ShouldReturnMatchingRecords()
	{
		// Arrange
		var withdrawn1 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "John",
			LastName = "Doe",
			MiddleInitial = "A",
			EmailAddress = "john.doe@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-1",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		var withdrawn2 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Jane",
			LastName = "Smith",
			MiddleInitial = "B",
			EmailAddress = "jane.smith@example.com",
			MobileNumber = "09171234568",
			HashToken = "hash-2",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Premium",
			RushNormal = "Rush",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddRangeAsync(withdrawn1, withdrawn2);
		await _dbContext.SaveChangesAsync();

		// Act
       var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.PaginationRequest(1, 10, "john"), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Data.Should().HaveCount(1);
		result.Data.First().FirstName.Should().Be("John");
		result.Data.First().EmailAddress.Should().Be("john.doe@example.com");
	}

	[Fact]
	public async Task SearchWithdrawnEmailInvitationRequests_ShouldSearchByLastName()
	{
		// Arrange
		var withdrawn1 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "John",
			LastName = "Doe",
			MiddleInitial = "A",
			EmailAddress = "john.doe@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-1",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		var withdrawn2 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Jane",
			LastName = "Doe",
			MiddleInitial = "B",
			EmailAddress = "jane.doe@example.com",
			MobileNumber = "09171234568",
			HashToken = "hash-2",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Premium",
			RushNormal = "Rush",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddRangeAsync(withdrawn1, withdrawn2);
		await _dbContext.SaveChangesAsync();

		// Act
       var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.PaginationRequest(1, 10, "doe"), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Data.Should().HaveCount(2);
		result.Data.Should().AllSatisfy(x => x.LastName.Should().Be("Doe"));
	}

	[Fact]
	public async Task SearchWithdrawnEmailInvitationRequests_ShouldReturnEmptyWhenNoMatch()
	{
		// Arrange
		var withdrawn = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "John",
			LastName = "Doe",
			MiddleInitial = "A",
			EmailAddress = "john.doe@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-1",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(withdrawn);
		await _dbContext.SaveChangesAsync();

		// Act
       var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.PaginationRequest(1, 10, "nonexistent"), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Data.Should().BeEmpty();
        result.Count.Should().Be(0);
	}

	#endregion
}
