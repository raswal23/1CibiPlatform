using Auth.Data.Entities;
using Auth.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class RoleServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;

	public RoleServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task GetRolesAsync_ShouldReturnPaginatedResult()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: null);

		var roleData = new List<RolesDTO>
		{
			new RolesDTO(1, "SuperAdmin", "SuperAdmin"),
			new RolesDTO(2, "Admin", "Admin")
		};

		_fixture.MockAuthRepository
			.Setup(x => x.GetRolesPageAsync(null, null, 11, CancellationToken.None))
			.ReturnsAsync(roleData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountRolesAsync(null, CancellationToken.None))
			.ReturnsAsync(10);

		// Act
		var result = await _fixture.RoleService.GetRolesAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(10);
		result.NextCursor.Should().BeNull();
		result.Items.Should().BeEquivalentTo(roleData);
	}

	[Fact]
	public async Task GetRolesAsync_ShouldPassSearchTerm_WhenProvided()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "SuperAdmin");

		var roleData = new List<RolesDTO>
		{
			new RolesDTO(1, "SuperAdmin", "SuperAdmin"),
		};

		_fixture.MockAuthRepository
			.Setup(x => x.GetRolesPageAsync("SuperAdmin", null, 11, CancellationToken.None))
			.ReturnsAsync(roleData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountRolesAsync("SuperAdmin", CancellationToken.None))
			.ReturnsAsync(1);

		// Act
		var result = await _fixture.RoleService.GetRolesAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(1);
		result.Items.Should().BeEquivalentTo(roleData);
	}

	[Fact]
	public async Task GetRolesAsync_ShouldReturnFirstPage_WhenCursorIsMalformed()
	{
		// A garbage cursor must never throw — it silently restarts the walk.
		var paginationRequest = new KeysetPaginationRequest(Cursor: "not-base64!!!", PageSize: 10, SearchTerm: null);

		var roleData = new List<RolesDTO> { new RolesDTO(1, "SuperAdmin", "SuperAdmin") };

		_fixture.MockAuthRepository
			.Setup(x => x.GetRolesPageAsync(null, null, 11, CancellationToken.None))
			.ReturnsAsync(roleData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountRolesAsync(null, CancellationToken.None))
			.ReturnsAsync(1);

		// Act
		var result = await _fixture.RoleService.GetRolesAsync(paginationRequest, CancellationToken.None);

		// Assert: treated as first page — TotalCount populated, no seek anchor used.
		result.TotalCount.Should().Be(1);
		result.Items.Should().BeEquivalentTo(roleData);
	}

	[Fact]
	public async Task GetRolesAsync_ShouldMintDecodableCursor_WhenMorePagesExist()
	{
		// Arrange: repo returns pageSize + 1 rows, so a next cursor must be minted.
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 2, SearchTerm: null);

		var roleData = new List<RolesDTO>
		{
			new RolesDTO(1, "Admin", "Admin"),
			new RolesDTO(2, "SuperAdmin", "SuperAdmin"),
			new RolesDTO(3, "Viewer", "Viewer")
		};

		_fixture.MockAuthRepository
			.Setup(x => x.GetRolesPageAsync(null, null, 3, CancellationToken.None))
			.ReturnsAsync(roleData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountRolesAsync(null, CancellationToken.None))
			.ReturnsAsync(3);

		// Act
		var result = await _fixture.RoleService.GetRolesAsync(paginationRequest, CancellationToken.None);

		// Assert: trimmed to pageSize and the cursor round-trips to the last RoleId.
		result.Items.Should().HaveCount(2);
		var fields = CursorCodec.Decode(result.NextCursor, 1);
		fields.Should().Equal("2");
	}

	[Fact]
	public async Task DeleteRoleAsync_ShouldThrow_WhenNotFound()
	{
		// Arrange
		var roleId = 99;
		_fixture.MockAuthRepository
			.Setup(x => x.GetRoleAsync(roleId))
			.ReturnsAsync((AuthRole)null);

		// Act
		Func<Task> act = async () => await _fixture.RoleService.DeleteRoleAsync(roleId);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"Role with ID {roleId} was not found.");
	}

	[Fact]
	public async Task DeleteRoleAsync_ShouldReturnTrue_WhenDeleted()
	{
		// Arrange
		var roleId = 1;
		var existingRole = new AuthRole { RoleId = roleId, RoleName = "SuperAdmin", Description = "SuperAdmin"};

		_fixture.MockAuthRepository
			.Setup(x => x.GetRoleAsync(roleId))
			.ReturnsAsync(existingRole);

		_fixture.MockAuthRepository
			.Setup(x => x.DeleteRoleAsync(existingRole))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.RoleService.DeleteRoleAsync(roleId);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task AddRoleAsync_ShouldReturnTrue_WhenAdded()
	{
		// Arrange
		var role = new AddRoleDTO { RoleName = "SuperAdmin", Description = "SuperAdmin"};

		_fixture.MockAuthRepository
			.Setup(x => x.AddRoleAsync(role))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.RoleService.AddRoleAsync(role);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task EditRoleAsync_ShouldThrow_WhenRoleNotFound()
	{
		// Arrange
		var editDto = new EditRoleDTO { RoleId = 99, RoleName = "UpdatedRole", Description = "Updated"};

		_fixture.MockAuthRepository
			.Setup(x => x.GetRoleAsync(editDto.RoleId))
			.ReturnsAsync((AuthRole)null);

		// Act
		Func<Task> act = async () => await _fixture.RoleService.EditRoleAsync(editDto);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"Role with ID {editDto.RoleId} was not found.");
	}

	[Fact]
	public async Task EditApplicationAsync_ShouldReturnUpdatedDto_WhenSuccessful()
	{
		// Arrange
		var editDto = new EditRoleDTO { RoleId = 1, RoleName = "UpdatedRole", Description = "Updated"};
		var existingRole = new AuthRole { RoleId = 1, RoleName = "UpdatedRole", Description = "Old"};
		var updatedRole = new AuthRole { RoleId = 1, RoleName = "UpdatedRole", Description = "Updated"};

		_fixture.MockAuthRepository
			.Setup(x => x.GetRoleAsync(editDto.RoleId))
			.ReturnsAsync(existingRole);

		_fixture.MockAuthRepository
			.Setup(x => x.EditRoleAsync(existingRole))
			.ReturnsAsync(updatedRole);

		// Act
		var result = await _fixture.RoleService.EditRoleAsync(editDto);

		// Assert
		result.Should().NotBeNull();
		result.RoleId.Should().Be(updatedRole.RoleId);
		result.RoleName.Should().Be(updatedRole.RoleName);
		result.Description.Should().Be(updatedRole.Description);
	}
}