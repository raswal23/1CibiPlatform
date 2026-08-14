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

		var request = new PaginationRequest(PageIndex: 1, PageSize: 2);

		// Act
		var result = await _packageManagementService.GetPackagesAsync(request, CancellationToken.None);

		// Assert
		result.PageIndex.Should().Be(1);
		result.PageSize.Should().Be(2);
		result.Count.Should().Be(3);
		result.Data.Select(x => x.PackageName)
			.Should().Equal("Alpha Screening", "Middle Screening");
	}

	[Fact]
	public async Task GetPackagesAsync_ShouldSearchNameAndDescriptionCaseInsensitively_WhenSearchTermIsProvided()
	{
		// Arrange
		await AddPackagesAsync(
			("Basic Screening", "Entry-level checks"),
			("Executive Screening", "Premium leadership checks"),
			("Premium Screening", "Comprehensive checks"));

		var request = new PaginationRequest(PageIndex: 1, PageSize: 10, SearchTerm: "PREMIUM");

		// Act
		var result = await _packageManagementService.GetPackagesAsync(request, CancellationToken.None);

		// Assert
		result.Count.Should().Be(2);
		result.Data.Select(x => x.PackageName)
			.Should().Equal("Executive Screening", "Premium Screening");
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
