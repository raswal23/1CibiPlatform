using Auth.Data.Entities;
using Auth.DTO;
using Auth.Features.UserManagement.Command.AddRole;
using Auth.Features.UserManagement.Command.DeleteRole;
using Auth.Features.UserManagement.Command.EditRole;
using Auth.Features.UserManagement.Query.GetRoles;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class RoleIntegrationTests : BaseIntegrationTest
{
	public RoleIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	[Fact]
	public async Task GetRoles_ShouldReturnPaginatedRoleList()
	{
		// Arrange
		await SeedRoleData();

		var query = new GetRolesQueryRequest(Cursor: null, PageSize: 3);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.Roles.Items.Count.Should().Be(3);
	}

	[Fact]
	public async Task GetRoless_ShouldReturnRoleList_BasedOnSearchTerm()
	{
		// Arrange
		await SeedRoleData();

		var query = new GetRolesQueryRequest(Cursor: null, PageSize: 1, SearchTerm: "SuperAdmin");

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();

		result.Roles.TotalCount.Should().Be(1);
		result.Roles.Items.ElementAt(0).roleName.Should().Be("SuperAdmin");
		result.Roles.Items.ElementAt(0).Description.Should().Be("SuperAdmin");
	}

	[Fact]
	public async Task GetRoles_ShouldReturnEmptyList_WhenNoRolesExist()
	{
		// Arrange
		var query = new GetRolesQueryRequest(Cursor: null, PageSize: 5);
		// Act
		var result = await _sender.Send(query);
		// Assert
		result.Should().NotBeNull();
		result.Roles.TotalCount.Should().Be(0);
	}

	[Fact]
	public async Task GetRoles_ShouldReturnCorrectPage_WhenCursorAndSizeAreSpecified()
	{
		// Arrange
		await SeedRoleData();
		var query = new GetRolesQueryRequest(Cursor: null, PageSize: 2);

		// Act
		var page1 = await _sender.Send(query);
		page1.Roles.NextCursor.Should().NotBeNull();

		var page2 = await _sender.Send(query with { Cursor = page1.Roles.NextCursor });

		// Assert
		page1.Roles.Items.Count.Should().Be(2);
		page1.Roles.TotalCount.Should().Be(3);
		page2.Roles.TotalCount.Should().BeNull();
		page2.Roles.Items.Select(r => r.roleId)
			.Should().NotIntersectWith(page1.Roles.Items.Select(r => r.roleId));
	}

	[Fact]
	public async Task GetRoles_ShouldReturnNullNextCursor_WhenDataIsExhausted()
	{
		// Arrange
		await SeedRoleData();

		var query = new GetRolesQueryRequest(Cursor: null, PageSize: 2);

		// Act
		var page1 = await _sender.Send(query);
		var page2 = await _sender.Send(query with { Cursor = page1.Roles.NextCursor });

		// Assert
		page2.Roles.Items.Count.Should().Be(1);
		page2.Roles.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task AddRole_ShouldAddNewRoleSuccessfully()
	{
		var role = new AddRoleDTO
		{
			RoleName = "SuperAdmin",
			Description = "SuperAdmin"
		};
		// Arrange
		var command = new AddRoleCommand(role);

		// Act
		var result = await _sender.Send(command);

		// Assert

		result.Should().NotBeNull();
		result.isAdded.Should().BeTrue();
	}

	[Fact]
	public async Task EditRole_ShouldUpdateExistingRoleSuccessfully()
	{
		// Arrange
		await SeedRoleData();

		var existingRole = await _dbContext.AuthRoles
			.AsNoTracking()
			.FirstAsync(x => x.RoleId == 2);

		var role = new EditRoleDTO
		{
			RoleId = existingRole!.RoleId,
			RoleName = existingRole.RoleName + " Updated",
			Description = existingRole.Description + " Updated"
		};

		var command = new EditRoleCommand(role);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result!.role.RoleName.Should().Be(role.RoleName);
		result!.role.Description.Should().Be(role.Description);
	}

	[Fact]
	public async Task EditRole_ShouldThrow_WhenRoleDoesNotExist()
	{
		// Arrange
		var role = new EditRoleDTO
		{
			RoleId = 1,
			RoleName = "SuperAdmin",
			Description = "SuperAdmin"
		};
		var command = new EditRoleCommand(role);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Role with ID {role.RoleId} was not found."); ;
	}

	[Fact]
	public async Task DeleteRole_ShouldRemoveRoleSuccessfully()
	{
		// Arrange
		await SeedRoleData();
		var command = new DeleteRoleCommand(1);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.IsDeleted.Should().BeTrue();
	}

	[Fact]
	public async Task DeleteRole_ShouldThrow_WhenRoleDoesNotExist()
	{
		// Arrange
		var command = new DeleteRoleCommand(99);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Role with ID 99 was not found."); ;
	}

	private async Task SeedRoleData()
	{
		var roles = new List<AuthRole>
		{
			new AuthRole
			{
				RoleId = 1,
				RoleName = "SuperAdmin",
				Description = "SuperAdmin",
			},
			new AuthRole
			{
				RoleId = 2,
				RoleName = "Admin",
				Description = "Admin",
			},
			new AuthRole
			{
				RoleId = 3,
				RoleName = "User",
				Description = "User",
			},
		};
		_dbContext.AuthRoles.AddRange(roles);
		await _dbContext.SaveChangesAsync();
	}
}
