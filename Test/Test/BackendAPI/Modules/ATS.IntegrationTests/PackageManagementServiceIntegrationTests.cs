using ATS.Data.Entities;
using ATS.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class PackageManagementServiceIntegrationTests : BaseIntegrationTest
{
	public PackageManagementServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task AddPackageAsync_ShouldPersistTrimmedPackage_WhenPackageIsValid()
	{
		// Arrange
		var package = new AddPackageDTO
		{
			PackageName = "  Standard Screening  ",
			PackageDescription = "  Standard background screening package  ",
			IsActive = true,
			FollowUpEmail = 3
		};

		// Act
		var result = await _packageManagementService.AddPackageAsync(package, CancellationToken.None);

		// Assert
		result.Should().BeTrue();

		var persisted = await _dbContext.PackageDetails
			.AsNoTracking()
			.SingleAsync(x => x.PackageName == "Standard Screening");

		persisted.PackageDescription.Should().Be("Standard background screening package");
		persisted.IsActive.Should().BeTrue();
		persisted.FollowUpEmail.Should().Be(3);
		persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
		persisted.UpdatedAt.Should().BeCloseTo(persisted.CreatedAt, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public async Task GetPackagesAsync_ShouldReturnAlphabeticalPaginatedPackages_WhenSearchTermIsEmpty()
	{
		// Arrange
		await AddPackagesAsync(
			("Zulu Screening", "Third package"),
			("Alpha Screening", "First package"),
			("Middle Screening", "Second package"));

		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 2);

		// Act
		var result = await _packageManagementService.GetPackagesAsync(request, CancellationToken.None);

		// Assert
		// Four, not three: BaseIntegrationTest seeds one package after every truncate,
		// because orders now carry a foreign key to their package and could not
		// otherwise be inserted. It sorts last, so the paging assertions below are
		// unaffected apart from the extra row.
		result.TotalCount.Should().Be(4);
		result.Items.Select(x => x.PackageName)
			.Should().Equal("Alpha Screening", "Middle Screening");
		result.NextCursor.Should().NotBeNull();

		var secondPage = await _packageManagementService.GetPackagesAsync(
			new KeysetPaginationRequest(Cursor: result.NextCursor, PageSize: 2),
			CancellationToken.None);

		secondPage.TotalCount.Should().BeNull();
		secondPage.Items.Select(x => x.PackageName)
			.Should().Equal("Zulu Screening", DefaultPackageName);
		secondPage.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task GetPackagesAsync_ShouldSearchNameAndDescriptionCaseInsensitively_WhenSearchTermIsProvided()
	{
		// Arrange
		await AddPackagesAsync(
			("Basic Screening", "Entry-level checks"),
			("Executive Screening", "Premium leadership checks"),
			("Premium Screening", "Comprehensive checks"));

		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "PREMIUM");

		// Act
		var result = await _packageManagementService.GetPackagesAsync(request, CancellationToken.None);

		// Assert
		result.TotalCount.Should().Be(2);
		result.Items.Select(x => x.PackageName)
			.Should().Equal("Executive Screening", "Premium Screening");
		result.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task EditPackageAsync_ShouldUpdateAndReturnPackage_WhenPackageExists()
	{
		// Arrange
		await _packageManagementService.AddPackageAsync(new AddPackageDTO
		{
			PackageName = "Original Package",
			PackageDescription = "Original description",
			IsActive = true,
			FollowUpEmail = 1
		}, CancellationToken.None);

		var existing = await _dbContext.PackageDetails
			.AsNoTracking()
			.SingleAsync(x => x.PackageName == "Original Package");

		_dbContext.ChangeTracker.Clear();

		var request = new EditPackageDTO
		{
			PackageId = existing.PackageId,
			PackageName = "  Updated Package  ",
			PackageDescription = "  Updated description  ",
			IsActive = false,
			FollowUpEmail = 7
		};

		// Act
		var result = await _packageManagementService.EditPackageAsync(request, CancellationToken.None);

		// Assert
		result.PackageId.Should().Be(existing.PackageId);
		result.PackageName.Should().Be("Updated Package");
		result.PackageDescription.Should().Be("Updated description");
		result.IsActive.Should().BeFalse();
		result.FollowUpEmail.Should().Be(7);
		result.UpdatedAt.Should().BeOnOrAfter(existing.UpdatedAt);

		var persisted = await _dbContext.PackageDetails
			.AsNoTracking()
			.SingleAsync(x => x.PackageId == existing.PackageId);

		persisted.PackageName.Should().Be("Updated Package");
		persisted.PackageDescription.Should().Be("Updated description");
		persisted.IsActive.Should().BeFalse();
		persisted.FollowUpEmail.Should().Be(7);
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task EditPackageAsync_ShouldThrowNotFoundException_WhenPackageDoesNotExist()
	{
		// Arrange
		var request = new EditPackageDTO
		{
			PackageId = int.MaxValue,
			PackageName = "Missing Package",
			PackageDescription = "Missing description",
			IsActive = true,
			FollowUpEmail = 1
		};

		// Act
		Func<Task> act = () => _packageManagementService.EditPackageAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage($"Package with ID {int.MaxValue} was not found.");
	}

	[Fact]
	public async Task AddPackageAsync_ShouldThrowDbUpdateException_WhenPackageNameAlreadyExists()
	{
		// Arrange
		var package = new AddPackageDTO
		{
			PackageName = "Duplicate Package",
			PackageDescription = "First description",
			IsActive = true,
			FollowUpEmail = 2
		};

		await _packageManagementService.AddPackageAsync(package, CancellationToken.None);

		var duplicate = new AddPackageDTO
		{
			PackageName = "Duplicate Package",
			PackageDescription = "Second description",
			IsActive = false,
			FollowUpEmail = 4
		};

		// Act
		Func<Task> act = () => _packageManagementService.AddPackageAsync(duplicate, CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<DbUpdateException>();

		var persistedCount = await _dbContext.PackageDetails
			.AsNoTracking()
			.CountAsync(x => x.PackageName == "Duplicate Package");

		persistedCount.Should().Be(1);
	}

	#endregion

	// Orders reference their package by id, so a rename cannot break them - but they
	// also carry the name as a display label, which the report lists and search read
	// directly. Without this the label would silently disagree with the package.
	[Fact]
	public async Task EditPackageAsync_ShouldRefreshTheLabelOnOrders_WhenThePackageIsRenamed()
	{
		var order = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Juan",
			LastName = "Dela Cruz",
			EmailAddress = "juan@example.com",
			MobileNumber = "09171234567",
			PackageId = DefaultPackageId,
			SelectPackage = DefaultPackageName,
			RushNormal = "Normal",
			HashToken = Guid.NewGuid().ToString("N"),
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddHours(24),
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info",
			OrderCreatedAt = DateTime.UtcNow,

			// Set false so the assertion below proves the rename raised it.
			NeedsProjection = false
		};

		await _dbContext.EmailInvitationRequests.AddAsync(order);
		await _dbContext.SaveChangesAsync();

		var existing = await _dbContext.PackageDetails
			.AsNoTracking()
			.FirstAsync(x => x.PackageId == DefaultPackageId);

		await _packageManagementService.EditPackageAsync(new EditPackageDTO
		{
			PackageId = existing.PackageId,
			PackageName = "Renamed Package",
			PackageDescription = existing.PackageDescription,
			IsActive = existing.IsActive,
			FollowUpEmail = existing.FollowUpEmail
		}, CancellationToken.None);

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.SelectPackage.Should().Be("Renamed Package");

		// The applicant search row denormalises the package name, so it has to be
		// rebuilt on the projection job's next pass.
		saved.NeedsProjection.Should().BeTrue();

		// The relationship itself is untouched.
		saved.PackageId.Should().Be(DefaultPackageId);
	}

	private async Task AddPackagesAsync(params (string Name, string Description)[] packages)
	{
		foreach (var package in packages)
		{
			await _packageManagementService.AddPackageAsync(new AddPackageDTO
			{
				PackageName = package.Name,
				PackageDescription = package.Description,
				IsActive = true,
				FollowUpEmail = 1
			}, CancellationToken.None);
		}
	}
}
