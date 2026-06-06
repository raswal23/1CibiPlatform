using Auth.Data.Entities;
using Auth.DTO;
using Auth.Features.AccountAssignmentNotification;
using Auth.Features.UserManagement.Command.AddAppSubRole;
using Auth.Features.UserManagement.Command.DeleteAppSubRole;
using Auth.Features.UserManagement.Command.EditAppSubRole;
using Auth.Features.UserManagement.Query.GetAppSubRoles;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class AppSubRoleIntegrationTests : BaseIntegrationTest
{
	public AppSubRoleIntegrationTests(IntegrationTestWebAppFactory factory)
			: base(factory)
	{
	}

	[Fact]
	public async Task GetAppSubRoles_ShouldReturnPaginatedAppSubRolesList()
	{
		// Arrange
		await SeedAppSubRoleData();

		var query = new GetAppSubRolesQueryRequest(PageNumber: 1, PageSize: 3);
		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.AppSubRoles.Data.Count().Should().Be(3);
	}

	[Fact]
	public async Task GetAppSubRoles_ShouldReturnAppSubRoleList_BasedOnSearchTerm()
	{
		// Arrange
		await SeedAppSubRoleData();

		var query = new GetAppSubRolesQueryRequest(PageNumber: 1, PageSize: 1, SearchTerm: "1");

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();

		result.Should().NotBeNull();
	}

	[Fact]
	public async Task GetAppSubRoles_ShouldReturnEmptyList_WhenNoAppSubRoleExist()
	{
		// Arrange
		var query = new GetAppSubRolesQueryRequest(PageNumber: 1, PageSize: 5);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.AppSubRoles.Count.Should().Be(0);
	}

	[Fact]
	public async Task GetAppSubRoles_ShouldReturnCorrectPage_WhenPageNumberAndSizeAreSpecified()
	{
		// Arrange
		await SeedAppSubRoleData();
		var query = new GetAppSubRolesQueryRequest(PageNumber: 1, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.AppSubRoles.PageIndex.Should().Be(1);
		result.AppSubRoles.PageSize.Should().Be(2);
	}

	[Fact]
	public async Task GetAppSubRoles_ShouldReturnEmptyList_WhenPageNumberExceedsTotalPages()
	{
		// Arrange
		await SeedAppSubRoleData();

		var query = new GetAppSubRolesQueryRequest(PageNumber: 3, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.AppSubRoles.Data.Count().Should().Be(0);
	}

	[Fact]
	public async Task AddAppSubRole_ShouldAddNewAppSubRoleSuccessfully()
	{
		// Arrange
		var assignedByUser = new Authusers { Id = Guid.NewGuid(), Email = "admin@test.com" };
		var targetUser = new Authusers { Id = Guid.NewGuid(), Email = "user@test.com" };

		_dbContext.AuthUsers.AddRange(assignedByUser, targetUser);
		_dbContext.AuthApplications.Add(new AuthApplication { AppId = 1, AppName = "App 1" });
		_dbContext.AuthSubmenu.Add(new AuthSubMenu { SubMenuId = 1, SubMenuName = "Submenu 1" });
		_dbContext.AuthRoles.Add(new AuthRole { RoleId = 1, RoleName = "Role 1" });

		await _dbContext.SaveChangesAsync();

		var appSubRole = new AddAppSubRoleDTO
		{
			UserId = targetUser.Id,
			AppId = 1,
			SubMenuId = 1,
			RoleId = 1,
			AssignedBy = assignedByUser.Id
		};

		var command = new AddAppSubRoleCommand(appSubRole);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.isAdded.Should().BeTrue();
	}


	[Fact]
	public async Task EditAppSubRole_ShouldUpdateExistingAppSubRoleSuccessfully()
	{
		// Arrange
		await SeedAppSubRoleData(); 

		var existingAppSubRole = await _dbContext.AuthUserAppRoles
			.AsNoTracking()
			.FirstAsync(x => x.AppRoleId == 1);

		var editDto = new EditAppSubRoleDTO
		{
			AppSubRoleId = existingAppSubRole.AppRoleId,
			UserId = existingAppSubRole.UserId,
			AppId = existingAppSubRole.AppId,
			SubMenuId = existingAppSubRole.Submenu,
			RoleId = existingAppSubRole.RoleId
		};

		editDto.AppId = 2;
		editDto.SubMenuId = 2;
		editDto.RoleId = 2;

		var command = new EditAppSubRoleCommand(editDto);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result!.appSubRole.AppId.Should().Be(editDto.AppId);
		result!.appSubRole.Submenu.Should().Be(editDto.SubMenuId);
		result!.appSubRole.RoleId.Should().Be(editDto.RoleId);
		result!.appSubRole.UserId.Should().Be(editDto.UserId);
	}


	[Fact]
	public async Task EditAppSubRole_ShouldThrow_WhenAppSubRoleDoesNotExist()
	{
		// Arrange
		var editDto = new EditAppSubRoleDTO
		{
			AppSubRoleId = 999, // Non-existent ID
			UserId = Guid.NewGuid(),
			AppId = 1,
			SubMenuId = 1,
			RoleId = 1
		};
		var command = new EditAppSubRoleCommand(editDto);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act
			.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage($"AppSubRole with ID {editDto.AppSubRoleId} was not found.");
	}

	[Fact]
	public async Task DeleteAppSubRole_ShouldRemoveAppSubRoleSuccessfully()
	{
		// Arrange
		await SeedAppSubRoleData(); 

		var command = new DeleteAppSubRoleCommand(1);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.IsDeleted.Should().BeTrue();
	}

	[Fact]
	public async Task DeleteAppSubRole_ShouldThrow_WhenAppSubRoleDoesNotExist()
	{
		// Arrange
		var command = new DeleteAppSubRoleCommand(99);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage($"AppSubRole with ID 99 was not found."); ;
	}

	[Fact]
	public async Task SendToUserEmailAsync_ShouldReturnNotificationResponse_WhenSuccessful()
	{
		// Arrange
		var request = new AccountNotificationDTO
		{
			Gmail = "new@example.com",
			Application = "PhilSys",
			SubMenu = "IDV",
			Role = "SuperAdmin"
		};

		var command = new AccountNotificationCommand(request);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.IsSent.Should().BeTrue();
	}

	private async Task SeedAppSubRoleData()
	{
		// Seed Users (required!)
		var assignedByUser = new Authusers
		{
			Id = Guid.NewGuid(),
			Email = "admin@test.com",
			FirstName = "Admin"
		};

		var user1 = new Authusers { Id = Guid.NewGuid(), Email = "u1@test.com" };
		var user2 = new Authusers { Id = Guid.NewGuid(), Email = "u2@test.com" };
		var user3 = new Authusers { Id = Guid.NewGuid(), Email = "u3@test.com" };

		_dbContext.AuthUsers.AddRange(assignedByUser, user1, user2, user3);

		// Seed Applications
		_dbContext.AuthApplications.AddRange(
			new AuthApplication { AppId = 1, AppName = "App 1" },
			new AuthApplication { AppId = 2, AppName = "App 2" },
			new AuthApplication { AppId = 3, AppName = "App 3" }
		);

		// Seed SubMenus
		_dbContext.AuthSubmenu.AddRange(
			new AuthSubMenu { SubMenuId = 1, SubMenuName = "Submenu 1" },
			new AuthSubMenu { SubMenuId = 2, SubMenuName = "Submenu 2" },
			new AuthSubMenu { SubMenuId = 3, SubMenuName = "Submenu 3" }
		);

		// Seed Roles
		_dbContext.AuthRoles.AddRange(
			new AuthRole { RoleId = 1, RoleName = "Role 1" },
			new AuthRole { RoleId = 2, RoleName = "Role 2" },
			new AuthRole { RoleId = 3, RoleName = "Role 3" }
		);

		await _dbContext.SaveChangesAsync(); 

		// Seed AuthUserAppRoles
		_dbContext.AuthUserAppRoles.AddRange(
			new AuthUserAppRole
			{
				AppRoleId = 1,
				UserId = user1.Id,
				AppId = 1,
				Submenu = 1,
				RoleId = 1,
				AssignedBy = assignedByUser.Id
			},
			new AuthUserAppRole
			{
				AppRoleId = 2,
				UserId = user2.Id,
				AppId = 2,
				Submenu = 2,
				RoleId = 2,
				AssignedBy = assignedByUser.Id
			},
			new AuthUserAppRole
			{
				AppRoleId = 3,
				UserId = user3.Id,
				AppId = 3,
				Submenu = 3,
				RoleId = 3,
				AssignedBy = assignedByUser.Id
			}
		);

		await _dbContext.SaveChangesAsync();
	}

}
