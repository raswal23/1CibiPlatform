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
		var result = await _moduleManagementService.EditModuleAsync(request);

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

	#endregion

	#region Bad Path

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
		Func<Task> act = () => _moduleManagementService.EditModuleAsync(request);

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
}
