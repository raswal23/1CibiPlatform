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
		var paginationRequest = new PaginationRequest
		{
			PageIndex = 1,
			PageSize = 10,
			SearchTerm = null
		};

		var userData = new List<UsersDTO>
			{
				new UsersDTO(Guid.CreateVersion7(), "user1@example.com", "sample1" , "sample2" , null, false),
				new UsersDTO(Guid.CreateVersion7(), "user2@example.com", "sample3" , "sample4" , null, false)
			};

		var expectedResult = new PaginatedResult<UsersDTO>(1, 2, 10, userData);

		var mockAuthRepository = _fixture
			.MockAuthRepository
			.Setup(x => x.GetUserAsync(paginationRequest, CancellationToken.None))
			.ReturnsAsync(expectedResult);

		// Act
		var result = await _fixture.UserManagementService.GetUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.PageIndex.Should().Be(expectedResult.PageIndex);
		result.PageSize.Should().Be(expectedResult.PageSize);
		result.Count.Should().Be(expectedResult.Count);
		result.Data.Should().BeEquivalentTo(expectedResult.Data);
	}

	[Fact]
	public async Task GetUsersAsync_ShouldCallSearchUserAsync_WhenSearchTermProvided()
	{
		// Arrange
		var service = _fixture.UserManagementService;
		var paginationRequest = new PaginationRequest
		{
			PageIndex = 1,
			PageSize = 10,
			SearchTerm = "sample1"
		};

		var userData = new List<UsersDTO>
			{
				new UsersDTO(Guid.CreateVersion7(), "user1@example.com", "sample1" , "sample2" , null, false)
			};

		var expectedResult = new PaginatedResult<UsersDTO>(1, 2, 10, userData);

		var mockAuthRepository = _fixture
			.MockAuthRepository
			.Setup(x => x.SearchUserAsync(paginationRequest, CancellationToken.None))
			.ReturnsAsync(expectedResult);

		// Act
		var result = await _fixture.UserManagementService.GetUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.PageIndex.Should().Be(expectedResult.PageIndex);
		result.PageSize.Should().Be(expectedResult.PageSize);
		result.Count.Should().Be(expectedResult.Count);
		result.Data.Should().BeEquivalentTo(expectedResult.Data);
	}

	[Fact]
	public async Task GetUnApprovedUsersAsync_ShouldReturnPaginatedResult()
	{
		// Arrange
		var service = _fixture.UserManagementService;
		var paginationRequest = new PaginationRequest
		{
			PageIndex = 1,
			PageSize = 10,
			SearchTerm = null
		};

		var userData = new List<UsersDTO>
			{
				new UsersDTO(Guid.CreateVersion7(), "user1@example.com", "sample1" , "sample2" , null, false),
				new UsersDTO(Guid.CreateVersion7(), "user2@example.com", "sample3" , "sample4" , null, false)
			};

		var expectedResult = new PaginatedResult<UsersDTO>(1, 2, 10, userData);

		var mockAuthRepository = _fixture
			.MockAuthRepository
			.Setup(x => x.GetUnapprovedUserAsync(paginationRequest, CancellationToken.None))
			.ReturnsAsync(expectedResult);

		// Act
		var result = await _fixture.UserManagementService.GetUnApprovedUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.PageIndex.Should().Be(expectedResult.PageIndex);
		result.PageSize.Should().Be(expectedResult.PageSize);
		result.Count.Should().Be(expectedResult.Count);
		result.Data.Should().BeEquivalentTo(expectedResult.Data);
	}

	[Fact]
	public async Task GetUnApprovedUsersAsync_ShouldCallSearchUnApprovedUserAsync_WhenSearchTermProvided()
	{
		// Arrange
		var service = _fixture.UserManagementService;
		var paginationRequest = new PaginationRequest
		{
			PageIndex = 1,
			PageSize = 10,
			SearchTerm = "sample1"
		};

		var userData = new List<UsersDTO>
			{
				new UsersDTO(Guid.CreateVersion7(), "user1@example.com", "sample1" , "sample2" , null, false)
			};

		var expectedResult = new PaginatedResult<UsersDTO>(1, 2, 10, userData);

		var mockAuthRepository = _fixture
			.MockAuthRepository
			.Setup(x => x.SearchUnApprovedUserAsync(paginationRequest, CancellationToken.None))
			.ReturnsAsync(expectedResult);

		// Act
		var result = await _fixture.UserManagementService.GetUnApprovedUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.PageIndex.Should().Be(expectedResult.PageIndex);
		result.PageSize.Should().Be(expectedResult.PageSize);
		result.Count.Should().Be(expectedResult.Count);
		result.Data.Should().BeEquivalentTo(expectedResult.Data);
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
