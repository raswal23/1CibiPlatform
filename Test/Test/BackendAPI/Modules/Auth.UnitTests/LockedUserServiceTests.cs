using Auth.Data.Entities;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class LockedUserServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;
	public LockedUserServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task DeleteLockedUserAsync_ShouldDeleteLockedUser_WhenLockedUserExists()
	{
		// Arrange
		var lockedUserId = Guid.CreateVersion7();
		var lockedUser = new AuthAttempts { UserId = lockedUserId };
		_fixture.MockAuthRepository
			.Setup(x => x.GetLockedUserAsync(lockedUserId))
			.ReturnsAsync(lockedUser);
		_fixture.MockAuthRepository
			.Setup(x => x.DeleteLockedUserAsync(lockedUser))
			.ReturnsAsync(true);
		// Act
		var result = await _fixture.LockedUserService.DeleteLockedUserAsync(lockedUserId);
		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task DeleteLockedUserAsync_ShouldThrow_WhenLockedUserNotFound()
	{
		// Arrange
		var lockedUserId = Guid.CreateVersion7();
		_fixture.MockAuthRepository
			.Setup(x => x.GetLockedUserAsync(lockedUserId))
			.ReturnsAsync((AuthAttempts?)null);

		// Act
		Func<Task> act = async () => await _fixture.LockedUserService.DeleteLockedUserAsync(lockedUserId);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"Locked user with ID {lockedUserId} was not found.");
	}

	[Fact]
	public async Task GetLockedUsersAsync_ShouldCallGetLockedUsersAsync_WhenNoSearchTerm()
	{
		// Arrange
		var paginationRequest = new PaginationRequest
		{
			PageIndex = 1,
			PageSize = 10,
			SearchTerm = null
		};

		var attempts = new List<AuthAttempts>
		{
			new AuthAttempts { UserId = Guid.CreateVersion7(), Attempts = 1, CreatedAt = DateTime.UtcNow },
			new AuthAttempts { UserId = Guid.CreateVersion7(), Attempts = 2, CreatedAt = DateTime.UtcNow }
		};

		var expectedResult = new PaginatedResult<AuthAttempts>(1, 10, attempts.Count, attempts);

		_fixture.MockAuthRepository
			.Setup(x => x.GetLockedUsersAsync(paginationRequest, CancellationToken.None))
			.ReturnsAsync(expectedResult);

		// Act
		var result = await _fixture.LockedUserService.GetLockedUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.PageIndex.Should().Be(expectedResult.PageIndex);
		result.PageSize.Should().Be(expectedResult.PageSize);
		result.Count.Should().Be(expectedResult.Count);
		result.Data.Should().BeEquivalentTo(expectedResult.Data);
	}

	[Fact]
	public async Task GetLockedUsersAsync_ShouldCallSearchLockedUserAsync_WhenSearchTermProvided()
	{
		// Arrange
		var paginationRequest = new PaginationRequest
		{
			PageIndex = 1,
			PageSize = 10,
			SearchTerm = "test@example.com"
		};

		var attempts = new List<AuthAttempts>
		{
			new AuthAttempts { UserId = Guid.CreateVersion7(), Email = "test@example.com", Attempts = 4, CreatedAt = DateTime.UtcNow }
		};

		var expectedResult = new PaginatedResult<AuthAttempts>(1, 10, attempts.Count, attempts);

		_fixture.MockAuthRepository
			.Setup(x => x.SearchLockedUserAsync(paginationRequest, CancellationToken.None))
			.ReturnsAsync(expectedResult);

		// Act
		var result = await _fixture.LockedUserService.GetLockedUsersAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.PageIndex.Should().Be(expectedResult.PageIndex);
		result.PageSize.Should().Be(expectedResult.PageSize);
		result.Count.Should().Be(expectedResult.Count);
		result.Data.Should().BeEquivalentTo(expectedResult.Data);
	}
}