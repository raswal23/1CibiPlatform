using Auth.Data.Entities;
using Auth.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class SubMenuServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;

	public SubMenuServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task GetSubMenusAsync_ShouldReturnPaginatedResult_WhenNoSearchTerm()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: null);

		var data = new List<SubMenusDTO>
		{
			new SubMenusDTO ( 1, "Dashboard", "CNX Dashboard", true),
			new SubMenusDTO ( 2, "IDV", "PhilSys IDV", true)
		};

		_fixture.MockAuthRepository
			.Setup(x => x.GetSubMenusPageAsync(null, null, 11, CancellationToken.None))
			.ReturnsAsync(data.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountSubMenusAsync(null, CancellationToken.None))
			.ReturnsAsync(10);

		// Act
		var result = await _fixture.SubMenuService.GetSubMenusAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(10);
		result.Items.Should().BeEquivalentTo(data);
	}

	[Fact]
	public async Task GetSubMenusAsync_ShouldPassSearchTerm_WhenProvided()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "Dashboard");

		var data = new List<SubMenusDTO>
		{
			new SubMenusDTO ( 1, "Dashboard", "CNX Dashboard" , true),
			new SubMenusDTO ( 2, "IDV", "PhilSys IDV", true)
		};

		_fixture.MockAuthRepository
			.Setup(x => x.GetSubMenusPageAsync("Dashboard", null, 11, CancellationToken.None))
			.ReturnsAsync(data.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountSubMenusAsync("Dashboard", CancellationToken.None))
			.ReturnsAsync(10);

		// Act
		var result = await _fixture.SubMenuService.GetSubMenusAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(10);
		result.Items.Should().BeEquivalentTo(data);
	}

	[Fact]
	public async Task AddSubMenuAsync_ShouldReturnTrue_WhenSuccessful()
	{
		// Arrange
		var subMenu = new AddSubMenuDTO { SubMenuName = "Dashboard", Description = "CNX Dashboard", IsActive = true };

		_fixture.MockAuthRepository
			.Setup(x => x.AddSubMenuAsync(subMenu))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.SubMenuService.AddSubMenuAsync(subMenu);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task EditSubMenuAsync_ShouldThrow_WhenSubMenuNotFound()
	{
		// Arrange
		var editDto = new EditSubMenuDTO { SubMenuId = 99, SubMenuName = "Updated", IsActive = false};

		_fixture.MockAuthRepository
			.Setup(x => x.GetSubMenuAsync(editDto.SubMenuId))
			.ReturnsAsync((AuthSubMenu)null);

		// Act
		Func<Task> act = async () => await _fixture.SubMenuService.EditSubMenuAsync(editDto);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"SubMenu with ID {editDto.SubMenuId} was not found.");
	}

	[Fact]
	public async Task EditSubMenuAsync_ShouldReturnUpdatedDto_WhenSuccessful()
	{
		// Arrange
		var editDto = new EditSubMenuDTO { SubMenuId = 1, SubMenuName = "UpdatedSubMenu", Description = "Updated", IsActive = false };
		var existingSubMenu = new AuthSubMenu { SubMenuId = 1, SubMenuName = "OldSubmenu", Description = "Old", IsActive = true };
		var updatedSubMenu = new AuthSubMenu { SubMenuId = 1, SubMenuName = "UpdatedSubMenu", Description = "Updated", IsActive = false };

		_fixture.MockAuthRepository
			.Setup(x => x.GetSubMenuAsync(editDto.SubMenuId))
			.ReturnsAsync(existingSubMenu);

		_fixture.MockAuthRepository
			.Setup(x => x.EditSubMenuAsync(existingSubMenu))
			.ReturnsAsync(updatedSubMenu);

		// Act
		var result = await _fixture.SubMenuService.EditSubMenuAsync(editDto);

		// Assert
		result.Should().NotBeNull();
		result.SubMenuId.Should().Be(updatedSubMenu.SubMenuId);
		result.SubMenuName.Should().Be(updatedSubMenu.SubMenuName);
	}

	[Fact]
	public async Task DeleteSubMenuAsync_ShouldThrow_WhenNotFound()
	{
		// Arrange
		var subMenuId = 99;

		_fixture.MockAuthRepository
			.Setup(x => x.GetSubMenuAsync(subMenuId))
			.ReturnsAsync((AuthSubMenu)null);

		// Act
		Func<Task> act = async () => await _fixture.SubMenuService.DeleteSubMenuAsync(subMenuId);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"SubMenu with ID {subMenuId} was not found.");
	}

	[Fact]
	public async Task DeleteSubMenuAsync_ShouldReturnTrue_WhenSuccessful()
	{
		// Arrange
		var subMenuId = 1;
		var existingSubMenu = new AuthSubMenu { SubMenuId = subMenuId, SubMenuName = "Dashboard", Description = "CNX Dashboard", IsActive = true};

		_fixture.MockAuthRepository
			.Setup(x => x.GetSubMenuAsync(subMenuId))
			.ReturnsAsync(existingSubMenu);

		_fixture.MockAuthRepository
			.Setup(x => x.DeleteSubMenuAsync(existingSubMenu))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.SubMenuService.DeleteSubMenuAsync(subMenuId);

		// Assert
		result.Should().BeTrue();
	}
}
