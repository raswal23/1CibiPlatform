using Auth.Data.Entities;
using Auth.DTO;
using Auth.Features.UserManagement.Command.AddSubMenu;
using Auth.Features.UserManagement.Command.DeleteSubMenu;
using Auth.Features.UserManagement.Command.EditSubMenu;
using Auth.Features.UserManagement.Query.GetSubMenus;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class SubMenuIntegrationTests : BaseIntegrationTest
{
	public SubMenuIntegrationTests(IntegrationTestWebAppFactory factory)
			: base(factory)
	{
	}

	[Fact]
	public async Task GetSubMenus_ShouldReturnPaginatedSubMenusList()
	{
		// Arrange
		await SeedSubMenuData();

		var query = new GetSubMenusQueryRequest(PageNumber: 1, PageSize: 3);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.subMenus.Data.Count().Should().Be(3);
	}

	[Fact]
	public async Task GetSubMenus_ShouldReturnSubMenuList_BasedOnSearchTerm()
	{
		// Arrange
		await SeedSubMenuData();

		var query = new GetSubMenusQueryRequest(PageNumber: 1, PageSize: 1, SearchTerm: "Dashboard");

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();

		result.subMenus.Count.Should().Be(1);
		result.subMenus.Data.ElementAt(0).subMenuName.Should().Be("Dashboard");
		result.subMenus.Data.ElementAt(0).Description.Should().Be("CNX Dashboard");
	}

	[Fact]
	public async Task GetSubMenus_ShouldReturnEmptyList_WhenNoSubMenusExist()
	{
		// Arrange
		var query = new GetSubMenusQueryRequest(PageNumber: 1, PageSize: 5);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.subMenus.Count.Should().Be(0);
	}

	[Fact]
	public async Task GetSubMenus_ShouldReturnCorrectPage_WhenPageNumberAndSizeAreSpecified()
	{
		// Arrange
		await SeedSubMenuData();
		var query = new GetSubMenusQueryRequest(PageNumber: 1, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.subMenus.PageIndex.Should().Be(1);
		result.subMenus.PageSize.Should().Be(2);
	}

	[Fact]
	public async Task GetSubMenus_ShouldReturnEmptyList_WhenPageNumberExceedsTotalPages()
	{
		// Arrange
		await SeedSubMenuData();

		var query = new GetSubMenusQueryRequest(PageNumber: 3, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.subMenus.Data.Count().Should().Be(0);
	}

	[Fact]
	public async Task AddSubMenu_ShouldAddNewSubMenuSuccessfully()
	{
		// Arrange
		var subMenu = new AddSubMenuDTO
		{
			SubMenuName = "NewSubMenu",
			Description = "New SubMenu",
			IsActive = true
		};
	
		var command = new AddSubMenuCommand(subMenu);

		// Act
		var result = await _sender.Send(command);

		// Assert

		result.Should().NotBeNull();
		result.isAdded.Should().BeTrue();
	}

	[Fact]
	public async Task EditSubMenu_ShouldUpdateExistingSubMenuSuccessfully()
	{
		// Arrange
		await SeedSubMenuData();

		var existingSubMenu = await _dbContext.AuthSubmenu
			.AsNoTracking()
			.FirstAsync(x => x.SubMenuId == 2);

		var subMenu = new EditSubMenuDTO
		{
			SubMenuId = existingSubMenu!.SubMenuId,
			SubMenuName = existingSubMenu.SubMenuName + " Updated",
			Description = existingSubMenu.Description + " Updated",
			IsActive = false
		};

		var command = new EditSubMenuCommand(subMenu);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result!.subMenu.SubMenuName.Should().Be(subMenu.SubMenuName);
		result!.subMenu.Description.Should().Be(subMenu.Description); 
		result!.subMenu.IsActive.Should().BeFalse();
	}

	[Fact]
	public async Task EditSubMenu_ShouldThrow_WhenSubMenuDoesNotExist()
	{
		// Arrange
		var subMenu = new EditSubMenuDTO
		{
			SubMenuId = 1,
			SubMenuName = "Dashboard",
			Description = "CNX Dashboard",
			IsActive = false
		};
		var command = new EditSubMenuCommand(subMenu);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage($"SubMenu with ID {subMenu.SubMenuId} was not found."); ;
	}

	[Fact]
	public async Task DeleteSubMenu_ShouldRemoveSubMenuSuccessfully()
	{
		// Arrange
		await SeedSubMenuData();
		var command = new DeleteSubMenuCommand(1);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.IsDeleted.Should().BeTrue();
	}

	[Fact]
	public async Task DeleteSubMenu_ShouldThrow_WhenSubMenuDoesNotExist()
	{
		// Arrange
		var command = new DeleteSubMenuCommand(99);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage($"SubMenu with ID 99 was not found."); ;
	}

	private async Task SeedSubMenuData()
	{
		var subMenus = new List<AuthSubMenu>
		{
			new AuthSubMenu
			{
				SubMenuId = 1,
				SubMenuName = "Dashboard",
				Description = "CNX Dashboard",
				IsActive = true
			},
			new AuthSubMenu
			{
				SubMenuId = 2,
				SubMenuName = "IDV",
				Description = "PhilSys IDV",
				IsActive = true
			},
			new AuthSubMenu
			{
				SubMenuId = 3,
				SubMenuName = "User Management",
				Description = "User Management",
				IsActive = true
			},
		};
		_dbContext.AuthSubmenu.AddRange(subMenus);
		await _dbContext.SaveChangesAsync();
	}
}
