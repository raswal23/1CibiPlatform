using ATS.Data.Entities;
using ATS.Data.Extensions;
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

		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 2);

		// Act
		var result = await _roleManagementService.GetRolesAsync(request, CancellationToken.None);

		// Assert
		result.TotalCount.Should().Be(3);
		result.Items.Select(x => x.RoleName)
			.Should().Equal("Alpha Reviewer", "Middle Reviewer");
		result.NextCursor.Should().NotBeNull();

		var secondPage = await _roleManagementService.GetRolesAsync(
			new KeysetPaginationRequest(Cursor: result.NextCursor, PageSize: 2),
			CancellationToken.None);

		secondPage.TotalCount.Should().BeNull();
		secondPage.Items.Select(x => x.RoleName)
			.Should().Equal("Zulu Reviewer");
		secondPage.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task GetRolesAsync_ShouldSearchNameAndDescriptionCaseInsensitively_WhenSearchTermIsProvided()
	{
		// Arrange
		await AddRolesAsync(
			("Basic Reviewer", "Reviews entry-level orders"),
			("Senior Reviewer", "Handles PREMIUM screening orders"),
			("Premium Specialist", "Handles complex orders"));

		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "premium");

		// Act
		var result = await _roleManagementService.GetRolesAsync(request, CancellationToken.None);

		// Assert
		result.TotalCount.Should().Be(2);
		result.Items.Select(x => x.RoleName)
			.Should().Equal("Premium Specialist", "Senior Reviewer");
		result.NextCursor.Should().BeNull();
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
		var result = await _roleManagementService.EditRoleAsync(request, CancellationToken.None);

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

	[Fact]
	public async Task EditRoleAsync_ShouldDeactivateRole_WhenOnlyInactiveUsersHoldIt()
	{
		// Arrange
		var roleId = await AddRoleReturningIdAsync("Dormant Role");
		await SeedUserWithRoleAsync(roleId, isActive: false);

		_dbContext.ChangeTracker.Clear();

		var request = new EditRoleDTO
		{
			RoleId = roleId,
			RoleName = "Dormant Role",
			RoleDescription = "Held only by an inactive user",
			IsActive = false
		};

		// Act
		var result = await _roleManagementService.EditRoleAsync(request, CancellationToken.None);

		// Assert
		result.IsActive.Should().BeFalse();

		var persisted = await _dbContext.RoleDetails
			.AsNoTracking()
			.SingleAsync(x => x.RoleId == roleId);
		persisted.IsActive.Should().BeFalse();
	}

	[Fact]
	public async Task EditRoleAsync_ShouldSkipUsageGuard_WhenRoleStaysActive()
	{
		// Arrange
		var roleId = await AddRoleReturningIdAsync("Busy Role");
		await SeedUserWithRoleAsync(roleId, isActive: true);

		_dbContext.ChangeTracker.Clear();

		var request = new EditRoleDTO
		{
			RoleId = roleId,
			RoleName = "Busy Role Renamed",
			RoleDescription = "Renamed while in use",
			IsActive = true
		};

		// Act
		var result = await _roleManagementService.EditRoleAsync(request, CancellationToken.None);

		// Assert
		result.RoleName.Should().Be("Busy Role Renamed");
		result.IsActive.Should().BeTrue();
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task EditRoleAsync_ShouldThrowConflictException_WhenDeactivatingRoleHeldByActiveUsers()
	{
		// Arrange
		var roleId = await AddRoleReturningIdAsync("Occupied Role");
		await SeedUserWithRoleAsync(roleId, isActive: true);

		_dbContext.ChangeTracker.Clear();

		var request = new EditRoleDTO
		{
			RoleId = roleId,
			RoleName = "Occupied Role",
			RoleDescription = "Still held by an active user",
			IsActive = false
		};

		// Act
		Func<Task> act = () => _roleManagementService.EditRoleAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<ConflictException>()
			.WithMessage("Cannot disable this role: 1 active user currently holds it.");

		var persisted = await _dbContext.RoleDetails
			.AsNoTracking()
			.SingleAsync(x => x.RoleId == roleId);
		persisted.IsActive.Should().BeTrue();
	}

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
		Func<Task> act = () => _roleManagementService.EditRoleAsync(request, CancellationToken.None);

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

	#region Seeded Identity Sequences

	[Fact]
	public async Task AddRoleAsync_ShouldPersistRole_WhenSeededRolesHoldExplicitIds()
	{
		// The seed inserts RoleId 1-4 explicitly, and an explicit id does not advance
		// the identity sequence. Every deployed database therefore had a sequence still
		// pointing at 1, so the first role an admin created collided with the seeded
		// "Platform Manager" and surfaced as the generic "error saving entity" popup.
		//
		// The tests never saw it: InitializeAsync truncates with RESTART IDENTITY, which
		// leaves the sequence agreeing with the (empty) table. So reproduce a seeded
		// database explicitly rather than trusting the clean one.
		await SeedRolesWithExplicitIdsAsync();

		// Act
		var result = await _roleManagementService.AddRoleAsync(new AddRoleDTO
		{
			RoleName = "Screening Specialist",
			RoleDescription = "Reviews and completes screening orders",
			IsActive = true
		});

		// Assert
		result.Should().BeTrue();

		var persisted = await _dbContext.RoleDetails
			.AsNoTracking()
			.SingleAsync(role => role.RoleName == "Screening Specialist");

		// Past the seeded block, so nothing was overwritten and no id was reused.
		persisted.RoleId.Should().BeGreaterThan(4);

		var seededNames = await _dbContext.RoleDetails
			.AsNoTracking()
			.Where(role => role.RoleId <= 4)
			.CountAsync();

		seededNames.Should().Be(4);
	}

	[Fact]
	public async Task AddModuleAsync_ShouldPersistModule_WhenSeededModulesHoldExplicitIds()
	{
		// ModuleDetails is seeded the same way (the AtsModuleIds constants), so it has
		// the same fault. SeedATSSuperAdminAccess already set the sequence past module
		// 10, but the seeder then adds 11-15 without touching it again.
		await SeedModulesWithExplicitIdsAsync();

		// Act
		var result = await _moduleManagementService.AddModuleAsync(new AddModuleDTO
		{
			ModuleName = "Screening Console",
			ModuleDescription = "Added after the seeded modules",
			IsActive = true
		});

		// Assert
		result.Should().BeTrue();

		var persisted = await _dbContext.ModuleDetails
			.AsNoTracking()
			.SingleAsync(module => module.ModuleName == "Screening Console");

		persisted.ModuleId.Should().BeGreaterThan(15);
	}

	// Writes the rows the way the seeder does - explicit ids, so the identity sequence
	// is left behind - then runs the production sync, which is what the fix relies on.
	private async Task SeedRolesWithExplicitIdsAsync()
	{
		await _dbContext.Database.ExecuteSqlRawAsync(
			"""
			INSERT INTO ats."RoleDetails"
				("RoleId", "RoleName", "RoleDescription", "IsActive", "CreatedAt", "UpdatedAt")
			VALUES
				(1, 'Platform Manager', 'Platform manager role for ATS system.', TRUE, NOW(), NOW()),
				(2, 'Client Admin', 'Administrator role for ATS system.', TRUE, NOW(), NOW()),
				(3, 'Service Delivery', 'Service Delivery role for ATS system.', TRUE, NOW(), NOW()),
				(4, 'User', 'Basic user role for ATS system.', TRUE, NOW(), NOW());
			""");

		await ATSDatabaseExtensions.SyncIdentitySequencesAsync(_dbContext);
	}

	private async Task SeedModulesWithExplicitIdsAsync()
	{
		await _dbContext.Database.ExecuteSqlRawAsync(
			"""
			INSERT INTO ats."ModuleDetails"
				("ModuleId", "ModuleName", "ModuleDescription", "IsActive", "CreatedAt", "UpdatedAt")
			SELECT
				seeded_id,
				CONCAT('Seeded Module ', seeded_id),
				'Seeded with an explicit id',
				TRUE,
				NOW(),
				NOW()
			FROM generate_series(1, 15) AS seeded_id;
			""");

		await ATSDatabaseExtensions.SyncIdentitySequencesAsync(_dbContext);
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

	private async Task<int> AddRoleReturningIdAsync(string roleName)
	{
		await _roleManagementService.AddRoleAsync(new AddRoleDTO
		{
			RoleName = roleName,
			RoleDescription = $"Description for {roleName}",
			IsActive = true
		});

		return await _dbContext.RoleDetails
			.AsNoTracking()
			.Where(role => role.RoleName == roleName)
			.Select(role => role.RoleId)
			.SingleAsync();
	}

	// UserDetails carries FKs to both role and module, so holding a role needs a
	// module row too.
	private async Task SeedUserWithRoleAsync(int roleId, bool isActive)
	{
		var now = DateTime.UtcNow;
		var module = new ModuleDetails
		{
			ModuleName = $"Role Guard Module {Guid.CreateVersion7()}",
			ModuleDescription = "Seeded for the role deactivation guard",
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};
		_dbContext.ModuleDetails.Add(module);
		await _dbContext.SaveChangesAsync();

		_dbContext.UserDetails.Add(new UserDetails
		{
			UserId = Guid.CreateVersion7(),
			UserName = "Role Guard User",
			UserEmail = "role.guard@cibi.com.ph",
			IsActive = isActive,
			Site = "Integration Test Site",
			RoleId = roleId,
			ModuleId = module.ModuleId,
			CreatedAt = now,
			UpdatedAt = now
		});
		await _dbContext.SaveChangesAsync();
	}
}
