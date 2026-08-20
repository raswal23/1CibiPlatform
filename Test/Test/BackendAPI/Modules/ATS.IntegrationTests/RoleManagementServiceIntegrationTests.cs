using ATS.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class RoleManagementServiceIntegrationTests : BaseIntegrationTest
{
	public RoleManagementServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task AddRoleAsync_ShouldPersistRole_WhenRoleIsValid()
	{
		// Arrange
		var role = new AddRoleDTO
		{
			RoleName = "Screening Specialist",
			RoleDescription = "Reviews and completes screening orders",
			IsActive = true
		};

		// Act
		var result = await _roleManagementService.AddRoleAsync(role);

		// Assert
		result.Should().BeTrue();

		var persisted = await _dbContext.RoleDetails
			.AsNoTracking()
			.SingleAsync(x => x.RoleName == "Screening Specialist");

		persisted.RoleDescription.Should().Be("Reviews and completes screening orders");
		persisted.IsActive.Should().BeTrue();
		persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
		persisted.UpdatedAt.Should().BeCloseTo(persisted.CreatedAt, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public async Task GetRolesAsync_ShouldReturnAlphabeticalPaginatedRoles_WhenSearchTermIsEmpty()
	{
		// Arrange
		await AddRolesAsync(
			("Zulu Reviewer", "Third role"),
			("Alpha Reviewer", "First role"),
			("Middle Reviewer", "Second role"));

		var request = new PaginationRequest(PageIndex: 1, PageSize: 2);

		// Act
		var result = await _roleManagementService.GetRolesAsync(request, CancellationToken.None);

		// Assert
		result.PageIndex.Should().Be(1);
		result.PageSize.Should().Be(2);
		result.Count.Should().Be(3);
		result.Data.Select(x => x.RoleName)
			.Should().Equal("Alpha Reviewer", "Middle Reviewer");
	}

	[Fact]
	public async Task GetRolesAsync_ShouldSearchNameAndDescriptionCaseInsensitively_WhenSearchTermIsProvided()
	{
		// Arrange
		await AddRolesAsync(
			("Basic Reviewer", "Reviews entry-level orders"),
			("Senior Reviewer", "Handles PREMIUM screening orders"),
			("Premium Specialist", "Handles complex orders"));

		var request = new PaginationRequest(PageIndex: 1, PageSize: 10, SearchTerm: "premium");

		// Act
		var result = await _roleManagementService.GetRolesAsync(request, CancellationToken.None);

		// Assert
		result.Count.Should().Be(2);
		result.Data.Select(x => x.RoleName)
			.Should().Equal("Premium Specialist", "Senior Reviewer");
	}

	[Fact]
	public async Task EditRoleAsync_ShouldUpdateAndReturnRole_WhenRoleExists()
	{
		// Arrange
		await _roleManagementService.AddRoleAsync(new AddRoleDTO
		{
			RoleName = "Original Role",
			RoleDescription = "Original description",
			IsActive = true
		});

		var existing = await _dbContext.RoleDetails
			.AsNoTracking()
			.SingleAsync(x => x.RoleName == "Original Role");

		_dbContext.ChangeTracker.Clear();

		var request = new EditRoleDTO
		{
			RoleId = existing.RoleId,
			RoleName = "Updated Role",
			RoleDescription = "Updated description",
			IsActive = false
		};

		// Act
		var result = await _roleManagementService.EditRoleAsync(request);

		// Assert
		result.RoleId.Should().Be(existing.RoleId);
		result.RoleName.Should().Be("Updated Role");
		result.RoleDescription.Should().Be("Updated description");
		result.IsActive.Should().BeFalse();
		result.UpdatedAt.Should().BeOnOrAfter(existing.UpdatedAt);

		var persisted = await _dbContext.RoleDetails
			.AsNoTracking()
			.SingleAsync(x => x.RoleId == existing.RoleId);

		persisted.RoleName.Should().Be("Updated Role");
		persisted.RoleDescription.Should().Be("Updated description");
		persisted.IsActive.Should().BeFalse();
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task EditRoleAsync_ShouldThrowNotFoundException_WhenRoleDoesNotExist()
	{
		// Arrange
		var request = new EditRoleDTO
		{
			RoleId = int.MaxValue,
			RoleName = "Missing Role",
			RoleDescription = "Missing description",
			IsActive = true
		};

		// Act
		Func<Task> act = () => _roleManagementService.EditRoleAsync(request);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage($"Role with ID {int.MaxValue} was not found.");
	}

	[Fact]
	public async Task AddRoleAsync_ShouldThrowDbUpdateException_WhenRoleNameAlreadyExists()
	{
		// Arrange
		var role = new AddRoleDTO
		{
			RoleName = "Duplicate Role",
			RoleDescription = "First description",
			IsActive = true
		};

		await _roleManagementService.AddRoleAsync(role);

		var duplicate = new AddRoleDTO
		{
			RoleName = "Duplicate Role",
			RoleDescription = "Second description",
			IsActive = false
		};

		// Act
		Func<Task> act = () => _roleManagementService.AddRoleAsync(duplicate);

		// Assert
		await act.Should().ThrowAsync<DbUpdateException>();

		var persistedCount = await _dbContext.RoleDetails
			.AsNoTracking()
			.CountAsync(x => x.RoleName == "Duplicate Role");

		persistedCount.Should().Be(1);
	}

	#endregion

	private async Task AddRolesAsync(params (string Name, string Description)[] roles)
	{
		foreach (var role in roles)
		{
			await _roleManagementService.AddRoleAsync(new AddRoleDTO
			{
				RoleName = role.Name,
				RoleDescription = role.Description,
				IsActive = true
			});
		}
	}
}
