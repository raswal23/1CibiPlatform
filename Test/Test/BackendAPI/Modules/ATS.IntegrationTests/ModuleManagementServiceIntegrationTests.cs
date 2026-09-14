using ATS.Data.Entities;
using ATS.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class ModuleManagementServiceIntegrationTests : BaseIntegrationTest
{
	public ModuleManagementServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task AddModuleAsync_ShouldPersistTrimmedModule_WhenModuleIsValid()
	{
		// Arrange
		var module = new AddModuleDTO
		{
			ModuleName = "  Dispute Management  ",
			ModuleDescription = "  Manages disputed screening orders  ",
			IsActive = true
		};

		// Act
		var result = await _moduleManagementService.AddModuleAsync(module);

		// Assert
		result.Should().BeTrue();

		var persisted = await _dbContext.ModuleDetails
			.AsNoTracking()
			.SingleAsync(x => x.ModuleName == "Dispute Management");

		persisted.ModuleDescription.Should().Be("Manages disputed screening orders");
		persisted.IsActive.Should().BeTrue();
		persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
		persisted.UpdatedAt.Should().BeCloseTo(persisted.CreatedAt, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public async Task GetModulesAsync_ShouldReturnAlphabeticalPaginatedModules_WhenSearchTermIsEmpty()
	{
		// Arrange
		await AddModulesAsync(
			("Withdrawn Orders", "Third module"),
			("Candidate Management", "First module"),
			("Search Reports", "Second module"));

		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 2);

		// Act
		var result = await _moduleManagementService.GetModulesAsync(request, CancellationToken.None);

		// Assert
		result.TotalCount.Should().Be(3);
		result.Items.Select(x => x.ModuleName)
			.Should().Equal("Candidate Management", "Search Reports");
		result.NextCursor.Should().NotBeNull();

		var secondPage = await _moduleManagementService.GetModulesAsync(
			new KeysetPaginationRequest(Cursor: result.NextCursor, PageSize: 2),
			CancellationToken.None);

		secondPage.TotalCount.Should().BeNull();
		secondPage.Items.Select(x => x.ModuleName)
			.Should().Equal("Withdrawn Orders");
		secondPage.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task GetModulesAsync_ShouldSearchNameAndDescriptionCaseInsensitively_WhenSearchTermIsProvided()
	{
		// Arrange
		await AddModulesAsync(
			("Basic Orders", "Handles entry-level screening"),
			("Report Search", "Finds PREMIUM screening reports"),
			("Premium Orders", "Handles complex screening"));

		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "premium");

		// Act
		var result = await _moduleManagementService.GetModulesAsync(request, CancellationToken.None);

		// Assert
		result.TotalCount.Should().Be(2);
		result.Items.Select(x => x.ModuleName)
			.Should().Equal("Premium Orders", "Report Search");
		result.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task EditModuleAsync_ShouldUpdateAndReturnModule_WhenModuleExists()
	{
		// Arrange
		await _moduleManagementService.AddModuleAsync(new AddModuleDTO
		{
			ModuleName = "Order Management",
			ModuleDescription = "Original description",
			IsActive = true
		});

		var existing = await _dbContext.ModuleDetails
			.AsNoTracking()
			.SingleAsync(x => x.ModuleName == "Order Management");

		_dbContext.ChangeTracker.Clear();

		var request = new EditModuleDTO
		{
			ModuleId = existing.ModuleId,
			ModuleName = existing.ModuleName,
			ModuleDescription = "Updated description",
			IsActive = false
		};

		// Act
		var result = await _moduleManagementService.EditModuleAsync(request, CancellationToken.None);

		// Assert
		result.ModuleId.Should().Be(existing.ModuleId);
		result.ModuleName.Should().Be("Order Management");
		result.ModuleDescription.Should().Be("Updated description");
		result.IsActive.Should().BeFalse();
		result.UpdatedAt.Should().BeOnOrAfter(existing.UpdatedAt);

		var persisted = await _dbContext.ModuleDetails
			.AsNoTracking()
			.SingleAsync(x => x.ModuleId == existing.ModuleId);

		persisted.ModuleName.Should().Be("Order Management");
		persisted.ModuleDescription.Should().Be("Updated description");
		persisted.IsActive.Should().BeFalse();
	}

	[Fact]
	public async Task EditModuleAsync_ShouldDeactivateModule_WhenOnlyInactiveUsersHaveIt()
	{
		// Arrange
		var moduleId = await AddModuleReturningIdAsync("Dormant Module");
		await SeedUserWithModuleAsync(moduleId, isActive: false);

		_dbContext.ChangeTracker.Clear();

		var request = new EditModuleDTO
		{
			ModuleId = moduleId,
			ModuleName = "Dormant Module",
			ModuleDescription = "Held only by an inactive user",
			IsActive = false
		};

		// Act
		var result = await _moduleManagementService.EditModuleAsync(request, CancellationToken.None);

		// Assert
		result.IsActive.Should().BeFalse();

		var persisted = await _dbContext.ModuleDetails
			.AsNoTracking()
			.SingleAsync(x => x.ModuleId == moduleId);
		persisted.IsActive.Should().BeFalse();
	}

	[Fact]
	public async Task EditModuleAsync_ShouldSkipUsageGuard_WhenModuleStaysActive()
	{
		// Arrange
		var moduleId = await AddModuleReturningIdAsync("Busy Module");
		await SeedUserWithModuleAsync(moduleId, isActive: true);

		_dbContext.ChangeTracker.Clear();

		var request = new EditModuleDTO
		{
			ModuleId = moduleId,
			ModuleName = "Busy Module Renamed",
			ModuleDescription = "Renamed while in use",
			IsActive = true
		};

		// Act
		var result = await _moduleManagementService.EditModuleAsync(request, CancellationToken.None);

		// Assert
		result.ModuleName.Should().Be("Busy Module Renamed");
		result.IsActive.Should().BeTrue();
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task EditModuleAsync_ShouldThrowConflictException_WhenDeactivatingModuleHeldByActiveUsers()
	{
		// Arrange
		var moduleId = await AddModuleReturningIdAsync("Occupied Module");
		await SeedUserWithModuleAsync(moduleId, isActive: true);

		_dbContext.ChangeTracker.Clear();

		var request = new EditModuleDTO
		{
			ModuleId = moduleId,
			ModuleName = "Occupied Module",
			ModuleDescription = "Still held by an active user",
			IsActive = false
		};

		// Act
		Func<Task> act = () => _moduleManagementService.EditModuleAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<ConflictException>()
			.WithMessage("Cannot disable this module: 1 active user currently has it.");

		var persisted = await _dbContext.ModuleDetails
			.AsNoTracking()
			.SingleAsync(x => x.ModuleId == moduleId);
		persisted.IsActive.Should().BeTrue();
	}

	[Fact]
	public async Task EditModuleAsync_ShouldThrowNotFoundException_WhenModuleDoesNotExist()
	{
		// Arrange
		var request = new EditModuleDTO
		{
			ModuleId = int.MaxValue,
			ModuleName = "Missing Module",
			ModuleDescription = "Missing description",
			IsActive = true
		};

		// Act
		Func<Task> act = () => _moduleManagementService.EditModuleAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage($"Module with ID {int.MaxValue} was not found.");
	}

	[Fact]
	public async Task AddModuleAsync_ShouldThrowBadRequestException_WhenModuleNameAlreadyExists()
	{
		// Arrange
		await _moduleManagementService.AddModuleAsync(new AddModuleDTO
		{
			ModuleName = "Duplicate Module",
			ModuleDescription = "First description",
			IsActive = true
		});

		var duplicate = new AddModuleDTO
		{
			ModuleName = "  duplicate module  ",
			ModuleDescription = "Second description",
			IsActive = false
		};

		// Act
		Func<Task> act = () => _moduleManagementService.AddModuleAsync(duplicate);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("Module 'duplicate module' already exists.");

		var persistedCount = await _dbContext.ModuleDetails
			.AsNoTracking()
			.CountAsync(x => x.ModuleName == "Duplicate Module");

		persistedCount.Should().Be(1);
	}

	#endregion

	private async Task AddModulesAsync(params (string Name, string Description)[] modules)
	{
		foreach (var module in modules)
		{
			await _moduleManagementService.AddModuleAsync(new AddModuleDTO
			{
				ModuleName = module.Name,
				ModuleDescription = module.Description,
				IsActive = true
			});
		}
	}

	private async Task<int> AddModuleReturningIdAsync(string moduleName)
	{
		await _moduleManagementService.AddModuleAsync(new AddModuleDTO
		{
			ModuleName = moduleName,
			ModuleDescription = $"Description for {moduleName}",
			IsActive = true
		});

		return await _dbContext.ModuleDetails
			.AsNoTracking()
			.Where(module => module.ModuleName == moduleName)
			.Select(module => module.ModuleId)
			.SingleAsync();
	}

	// UserDetails carries FKs to both role and module, so having a module needs a
	// role row too.
	private async Task SeedUserWithModuleAsync(int moduleId, bool isActive)
	{
		var now = DateTime.UtcNow;
		var role = new RoleDetails
		{
			RoleName = $"Module Guard Role {Guid.CreateVersion7()}",
			RoleDescription = "Seeded for the module deactivation guard",
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};
		_dbContext.RoleDetails.Add(role);
		await _dbContext.SaveChangesAsync();

		_dbContext.UserDetails.Add(new UserDetails
		{
			UserId = Guid.CreateVersion7(),
			UserName = "Module Guard User",
			UserEmail = "module.guard@cibi.com.ph",
			IsActive = isActive,
			Site = "Integration Test Site",
			RoleId = role.RoleId,
			ModuleId = moduleId,
			CreatedAt = now,
			UpdatedAt = now
		});
		await _dbContext.SaveChangesAsync();
	}
}
