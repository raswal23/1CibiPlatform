using Auth.Data.Entities;
using Auth.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class UserManagementServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;

	public UserManagementServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task GetUsersAsync_ShouldReturnPaginatedResult()
	{
		// Arrange
		var service = _fixture.UserManagementService;
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: null);

		// One active and one inactive: the User tab lists the whole registry, so the
		// service must pass both states through untouched.
		var userData = new List<UsersDTO>
			{
				new UsersDTO(Guid.CreateVersion7(), "user1@example.com", "sample1" , "sample2" , null, false, true),
				new UsersDTO(Guid.CreateVersion7(), "user2@example.com", "sample3" , "sample4" , null, false, false)
			};

		_fixture.MockAuthRepository
			.Setup(x => x.GetUsersPageAsync(null, null, 11, CancellationToken.None))
			.ReturnsAsync(userData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountUsersAsync(null, CancellationToken.None))
			.ReturnsAsync(10);

		// Act
		var result = await _fixture.UserManagementService.GetUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(10);
		result.Items.Should().BeEquivalentTo(userData);
	}

	[Fact]
	public async Task GetUsersAsync_ShouldPassSearchTerm_WhenProvided()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "sample1");

		var userData = new List<UsersDTO>
			{
				new UsersDTO(Guid.CreateVersion7(), "user1@example.com", "sample1" , "sample2" , null, false, true)
			};

		_fixture.MockAuthRepository
			.Setup(x => x.GetUsersPageAsync("sample1", null, 11, CancellationToken.None))
			.ReturnsAsync(userData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountUsersAsync("sample1", CancellationToken.None))
			.ReturnsAsync(1);

		// Act
		var result = await _fixture.UserManagementService.GetUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(1);
		result.Items.Should().BeEquivalentTo(userData);
	}

	[Fact]
	public async Task GetUnApprovedUsersAsync_ShouldReturnPaginatedResult()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: null);

		// Active but unapproved - the shape the Approval tab still filters for.
		var userData = new List<UsersDTO>
			{
				new UsersDTO(Guid.CreateVersion7(), "user1@example.com", "sample1" , "sample2" , null, false, true),
				new UsersDTO(Guid.CreateVersion7(), "user2@example.com", "sample3" , "sample4" , null, false, true)
			};

		_fixture.MockAuthRepository
			.Setup(x => x.GetUnapprovedUsersPageAsync(null, null, 11, CancellationToken.None))
			.ReturnsAsync(userData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountUnapprovedUsersAsync(null, CancellationToken.None))
			.ReturnsAsync(10);

		// Act
		var result = await _fixture.UserManagementService.GetUnApprovedUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(10);
		result.Items.Should().BeEquivalentTo(userData);
	}

	[Fact]
	public async Task GetUnApprovedUsersAsync_ShouldPassSearchTerm_WhenProvided()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "sample1");

		var userData = new List<UsersDTO>
			{
				new UsersDTO(Guid.CreateVersion7(), "user1@example.com", "sample1" , "sample2" , null, false, true)
			};

		_fixture.MockAuthRepository
			.Setup(x => x.GetUnapprovedUsersPageAsync("sample1", null, 11, CancellationToken.None))
			.ReturnsAsync(userData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountUnapprovedUsersAsync("sample1", CancellationToken.None))
			.ReturnsAsync(1);

		// Act
		var result = await _fixture.UserManagementService.GetUnApprovedUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(1);
		result.Items.Should().BeEquivalentTo(userData);
	}

	[Fact]
	public async Task EditUserAsync_ShouldThrow_WhenUserNotFound()
	{
		// Arrange
		var editDto = new EditUserDTO { Email = "john@example.com", IsApproved = true};

		_fixture.MockAuthRepository
			.Setup(x => x.GetUserAsync(editDto.Email))
			.ReturnsAsync((Authusers)null);

		// Act
		Func<Task> act = async () => await _fixture.UserManagementService.EditUserAsync(editDto);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"{editDto.Email} was not found.");
	}

	[Fact]
	public async Task EditUserAsync_ShouldReturnUpdatedDto_WhenSuccessful()
	{
		// Arrange
		var editDto = new EditUserDTO { Email = "johndoe@example.com", IsApproved = true };
		var existingUser = new Authusers { Email = "johndoe@example.com", IsApproved = false };
		var updatedUser = new Authusers { Email = "johndoe@example.com", IsApproved = true };

		_fixture.MockAuthRepository
			.Setup(x => x.GetUserAsync(editDto.Email))
			.ReturnsAsync(existingUser);

		_fixture.MockAuthRepository
			.Setup(x => x.EditUserAsync(existingUser))
			.ReturnsAsync(updatedUser);

		// Act
		var result = await _fixture.UserManagementService.EditUserAsync(editDto);

		// Assert
		result.Should().NotBeNull();
		result.IsApproved.Should().BeTrue();
	}

	[Fact]
	public async Task EditUserStatusAsync_ShouldDeactivateUser_WhenSuccessful()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var statusDto = new EditUserStatusDTO { UserId = userId, IsActive = false };
		var existingUser = new Authusers { Id = userId, Email = "johndoe@example.com", IsActive = true };

		_fixture.MockAuthRepository
			.Setup(x => x.GetUserByIdAsync(userId))
			.ReturnsAsync(existingUser);

		_fixture.MockAuthRepository
			.Setup(x => x.EditUserAsync(existingUser))
			.ReturnsAsync(existingUser);

		// Act
		var result = await _fixture.UserManagementService.EditUserStatusAsync(statusDto);

		// Assert
		result.Should().NotBeNull();
		existingUser.IsActive.Should().BeFalse();
	}

	// The case the filtered GetRawUserAsync could not serve: an inactive user is exactly
	// the one being reactivated, so loading them must not depend on their being active.
	[Fact]
	public async Task EditUserStatusAsync_ShouldReactivateUser_WhenTheUserIsInactive()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var statusDto = new EditUserStatusDTO { UserId = userId, IsActive = true };
		var existingUser = new Authusers { Id = userId, Email = "johndoe@example.com", IsActive = false };

		_fixture.MockAuthRepository
			.Setup(x => x.GetUserByIdAsync(userId))
			.ReturnsAsync(existingUser);

		_fixture.MockAuthRepository
			.Setup(x => x.EditUserAsync(existingUser))
			.ReturnsAsync(existingUser);

		// Act
		var result = await _fixture.UserManagementService.EditUserStatusAsync(statusDto);

		// Assert
		result.Should().NotBeNull();
		existingUser.IsActive.Should().BeTrue();
	}

	// Approval is the approval queue's decision; a status edit must not be a back door to it.
	[Fact]
	public async Task EditUserStatusAsync_ShouldLeaveApprovalUntouched()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var statusDto = new EditUserStatusDTO { UserId = userId, IsActive = false };
		var existingUser = new Authusers
		{
			Id = userId,
			Email = "johndoe@example.com",
			IsActive = true,
			IsApproved = true
		};

		_fixture.MockAuthRepository
			.Setup(x => x.GetUserByIdAsync(userId))
			.ReturnsAsync(existingUser);

		_fixture.MockAuthRepository
			.Setup(x => x.EditUserAsync(existingUser))
			.ReturnsAsync(existingUser);

		// Act
		await _fixture.UserManagementService.EditUserStatusAsync(statusDto);

		// Assert
		existingUser.IsApproved.Should().BeTrue();
	}

	[Fact]
	public async Task EditUserStatusAsync_ShouldThrow_WhenUserNotFound()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var statusDto = new EditUserStatusDTO { UserId = userId, IsActive = false };

		_fixture.MockAuthRepository
			.Setup(x => x.GetUserByIdAsync(userId))
			.ReturnsAsync((Authusers)null);

		// Act
		Func<Task> act = async () => await _fixture.UserManagementService.EditUserStatusAsync(statusDto);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"User {userId} was not found.");
	}

	[Fact]
	public async Task SendToUserEmailAsync_ShouldReturnNotificationResponse_WhenSuccessful()
	{
		// Arrange
		var request = new
		{
			Gmail = "new@example.com"
		};


		_fixture.MockEmailService.Setup(x => x.SendApprovalNotificationBody(request.Gmail)).Returns("body");
		_fixture.MockEmailService
			.Setup(x => x.SendEmailAsync(request.Gmail, "Account Assignment Notification", It.IsAny<string>(), It.IsAny<bool>()))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.UserManagementService.SendApprovalToUserEmailAsync(request.Gmail);

		// Assert
		result.Should().BeTrue();
	}
}
