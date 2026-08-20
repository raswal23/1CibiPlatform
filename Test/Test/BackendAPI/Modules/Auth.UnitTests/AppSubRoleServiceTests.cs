using Auth.Data.Entities;
using Auth.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class AppSubRoleServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;
	public AppSubRoleServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task GetAppSubRolesAsync_ShouldCallGetAppSubRoles_WhenNoSearchTerm()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(
			Cursor: null,
			PageSize: 10,
			SearchTerm: null);

		var data = new List<AppSubRolesDTO>
			{
				new AppSubRolesDTO ( 1, Guid.CreateVersion7(), "john@example.com", 1, "PhilSys", 1, "IDV", 1, "SuperAdmin" ),
				new AppSubRolesDTO ( 2, Guid.CreateVersion7(), "doe@example.com", 2, "CNX", 2, "DashBoard", 2, "Admin" )
			};

		_fixture.MockAuthRepository
			.Setup(x => x.GetAppSubRolesPageAsync(null, null, 11, CancellationToken.None))
			.ReturnsAsync(data.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountAppSubRolesAsync(null, CancellationToken.None))
			.ReturnsAsync(2);

		// Act
		var result = await _fixture.AppSubRoleService.GetAppSubRolesAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(2);
		result.NextCursor.Should().BeNull();
		result.Items.Should().BeEquivalentTo(data);
	}

	[Fact]
	public async Task GetAppSubRolesAsync_ShouldPassSearchTerm_WhenProvided()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(
			Cursor: null,
			PageSize: 10,
			SearchTerm: "1");

		var data = new List<AppSubRolesDTO>
			{
				new AppSubRolesDTO ( 1, Guid.CreateVersion7(), "john@example.com", 1, "PhilSys", 1, "IDV", 1, "SuperAdmin" ),
				new AppSubRolesDTO ( 2, Guid.CreateVersion7(), "doe@example.com", 2, "CNX", 2, "DashBoard", 2, "Admin" )
			};

		_fixture.MockAuthRepository
			.Setup(x => x.GetAppSubRolesPageAsync("1", null, 11, CancellationToken.None))
			.ReturnsAsync(data.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountAppSubRolesAsync("1", CancellationToken.None))
			.ReturnsAsync(2);

		// Act
		var result = await _fixture.AppSubRoleService.GetAppSubRolesAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(2);
		result.NextCursor.Should().BeNull();
		result.Items.Should().BeEquivalentTo(data);
	}

	[Fact]
	public async Task AddAppSubRoleAsync_ShouldReturnTrue_WhenSuccessful()
	{
		// Arrange
		var appSubRole = new AddAppSubRoleDTO { UserId = Guid.CreateVersion7(), AppId = 1, SubMenuId = 1, RoleId = 1, AssignedBy = Guid.CreateVersion7() };

		_fixture.MockAuthRepository
			.Setup(x => x.AddAppSubRoleAsync(appSubRole))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.AppSubRoleService.AddAppSubRoleAsync(appSubRole);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task EditAppSubRolesync_ShouldThrow_WhenAppSubRoleNotFound()
	{
		// Arrange
		var editDto = new EditAppSubRoleDTO { AppSubRoleId = 1, UserId = Guid.CreateVersion7(), AppId = 1, SubMenuId = 1, RoleId = 1 };

		_fixture.MockAuthRepository
			.Setup(x => x.GetAppSubRoleAsync(editDto.AppSubRoleId))
			.ReturnsAsync((AuthUserAppRole?)null);

		// Act
		Func<Task> act = async () => await _fixture.AppSubRoleService.EditAppSubRoleAsync(editDto);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"AppSubRole with ID {editDto.AppSubRoleId} was not found.");
	}

	[Fact]
	public async Task EditAppSubRolesync_ShouldReturnUpdatedDto_WhenSuccessful()
	{
		// Arrange
		var editDto = new EditAppSubRoleDTO { AppSubRoleId = 1, UserId = Guid.CreateVersion7(), AppId = 2, SubMenuId = 2, RoleId = 2 };
		var existingAppSubRole = new AuthUserAppRole { AppRoleId = 1, UserId = Guid.CreateVersion7(), AppId = 1, Submenu = 1, RoleId = 1 };
		var updatedAppSubRole = new AuthUserAppRole { AppRoleId = 1, UserId = Guid.CreateVersion7(), AppId = 2, Submenu = 2, RoleId = 2 };

		_fixture.MockAuthRepository
			.Setup(x => x.GetAppSubRoleAsync(editDto.AppSubRoleId))
			.ReturnsAsync(existingAppSubRole);

		_fixture.MockAuthRepository
			.Setup(x => x.EditAppSubRoleAsync(existingAppSubRole))
			.ReturnsAsync(updatedAppSubRole);

		// Act
		var result = await _fixture.AppSubRoleService.EditAppSubRoleAsync(editDto);

		// Assert
		result.Should().NotBeNull();
		result.AppRoleId.Should().Be(updatedAppSubRole.AppRoleId);
		result.AppId.Should().Be(updatedAppSubRole.AppId);
		result.Submenu.Should().Be(updatedAppSubRole.Submenu);
		result.RoleId.Should().Be(updatedAppSubRole.RoleId);
	}

	[Fact]
	public async Task DeleteAppSubRoleAsync_ShouldThrow_WhenNotFound()
	{
		// Arrange
		var appSubRoleId = 99;

		_fixture.MockAuthRepository
			.Setup(x => x.GetAppSubRoleAsync(appSubRoleId))
			.ReturnsAsync((AuthUserAppRole?)null);

		// Act
		Func<Task> act = async () => await _fixture.AppSubRoleService.DeleteAppSubRoleAsync(appSubRoleId);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"AppSubRole with ID {appSubRoleId} was not found.");
	}

	[Fact]
	public async Task DeleteAppSubRoleAsync_ShouldReturnTrue_WhenSuccessful()
	{
		// Arrange
		var appSubRoleId = 1;
		var existingAppSubRole = new AuthUserAppRole { AppRoleId = appSubRoleId, UserId = Guid.CreateVersion7(), AppId = 1, Submenu = 1, RoleId = 1 };

		_fixture.MockAuthRepository
			.Setup(x => x.GetAppSubRoleAsync(appSubRoleId))
			.ReturnsAsync(existingAppSubRole);

		_fixture.MockAuthRepository
			.Setup(x => x.DeleteAppSubRoleAsync(existingAppSubRole))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.AppSubRoleService.DeleteAppSubRoleAsync(appSubRoleId);

		// Assert
		result.Should().BeTrue();
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


		_fixture.MockEmailService.Setup(x => x.SendNotificationBody(request.Gmail, request.Application, request.SubMenu, request.Role)).Returns("body");
		_fixture.MockEmailService
			.Setup(x => x.SendEmailAsync(request.Gmail, "Account Assignment Notification", It.IsAny<string>(), It.IsAny<bool>()))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.AppSubRoleService.SendToUserEmailAsync(request);

		// Assert
		result.Should().BeTrue();
	}
}

