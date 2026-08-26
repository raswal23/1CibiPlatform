using Auth.Data.Entities;
using Auth.Data.Repository;
using Auth.DTO;
using Auth.Services;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class UserProfileServiceTests
{
	private readonly Mock<IUserProfileRepository> _repository = new();
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly Mock<ILogger<UserProfileService>> _logger = new();

	private UserProfileService CreateService() =>
		new(_repository.Object, _currentUser.Object, _logger.Object);

	private void AuthenticateAs(Guid userId)
	{
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
	}

	private static Authusers CreateUser(Guid userId) =>
		new()
		{
			Id = userId,
			Email = "john@example.com",
			FirstName = "John",
			MiddleName = "Quincy",
			LastName = "Doe",
			IsActive = true,
			IsApproved = true
		};

	[Fact]
	public async Task GetMyProfileAsync_ShouldReturnProfile_WhenUserExists()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		AuthenticateAs(userId);

		_repository
			.Setup(repo => repo.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(CreateUser(userId));

		// Act
		var result = await CreateService().GetMyProfileAsync(CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.UserId.Should().Be(userId);
		result.Email.Should().Be("john@example.com");
		result.FirstName.Should().Be("John");
		result.MiddleName.Should().Be("Quincy");
		result.LastName.Should().Be("Doe");
		result.FullName.Should().Be("John Quincy Doe");
	}

	[Fact]
	public async Task GetMyProfileAsync_ShouldThrowUnauthorized_WhenCallerIsNotAuthenticated()
	{
		// Arrange
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(false);
		_currentUser.SetupGet(user => user.UserId).Returns((Guid?)null);

		// Act
		Func<Task> act = async () => await CreateService().GetMyProfileAsync(CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<UnauthorizedException>()
			.WithMessage("The current user is not authenticated.");
	}

	[Fact]
	public async Task GetMyProfileAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		AuthenticateAs(userId);

		_repository
			.Setup(repo => repo.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
			.ReturnsAsync((Authusers?)null);

		// Act
		Func<Task> act = async () => await CreateService().GetMyProfileAsync(CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage("The authenticated user profile was not found.");
	}

	[Fact]
	public async Task UpdateMyProfileAsync_ShouldPersistTrimmedNames_WhenSuccessful()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		AuthenticateAs(userId);

		var existingUser = CreateUser(userId);

		_repository
			.Setup(repo => repo.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(existingUser);
		_repository
			.Setup(repo => repo.UpdateProfileAsync(existingUser, It.IsAny<CancellationToken>()))
			.ReturnsAsync((Authusers user, CancellationToken _) => user);

		var update = new UpdateUserProfileDTO
		{
			FirstName = "  Jane  ",
			MiddleName = "  Marie ",
			LastName = " Smith "
		};

		// Act
		var result = await CreateService().UpdateMyProfileAsync(update, CancellationToken.None);

		// Assert
		result.FirstName.Should().Be("Jane");
		result.MiddleName.Should().Be("Marie");
		result.LastName.Should().Be("Smith");
		result.FullName.Should().Be("Jane Marie Smith");
		existingUser.FirstName.Should().Be("Jane");
		existingUser.LastName.Should().Be("Smith");
	}

	[Fact]
	public async Task UpdateMyProfileAsync_ShouldStoreNullMiddleName_WhenBlank()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		AuthenticateAs(userId);

		var existingUser = CreateUser(userId);

		_repository
			.Setup(repo => repo.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(existingUser);
		_repository
			.Setup(repo => repo.UpdateProfileAsync(existingUser, It.IsAny<CancellationToken>()))
			.ReturnsAsync((Authusers user, CancellationToken _) => user);

		var update = new UpdateUserProfileDTO
		{
			FirstName = "John",
			MiddleName = "   ",
			LastName = "Doe"
		};

		// Act
		var result = await CreateService().UpdateMyProfileAsync(update, CancellationToken.None);

		// Assert
		existingUser.MiddleName.Should().BeNull();
		result.MiddleName.Should().BeNull();
		result.FullName.Should().Be("John Doe");
	}

	[Fact]
	public async Task UpdateMyProfileAsync_ShouldUseTheAuthenticatedUserId_NotTheRequestPayload()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		AuthenticateAs(userId);

		var existingUser = CreateUser(userId);

		_repository
			.Setup(repo => repo.GetProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(existingUser);
		_repository
			.Setup(repo => repo.UpdateProfileAsync(existingUser, It.IsAny<CancellationToken>()))
			.ReturnsAsync((Authusers user, CancellationToken _) => user);

		var update = new UpdateUserProfileDTO
		{
			FirstName = "Jane",
			LastName = "Smith"
		};

		// Act
		await CreateService().UpdateMyProfileAsync(update, CancellationToken.None);

		// Assert
		_repository.Verify(
			repo => repo.GetProfileAsync(userId, It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task UpdateMyProfileAsync_ShouldThrowUnauthorized_WhenCallerIsNotAuthenticated()
	{
		// Arrange
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(false);
		_currentUser.SetupGet(user => user.UserId).Returns((Guid?)null);

		var update = new UpdateUserProfileDTO
		{
			FirstName = "Jane",
			LastName = "Smith"
		};

		// Act
		Func<Task> act = async () => await CreateService().UpdateMyProfileAsync(update, CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<UnauthorizedException>();
		_repository.Verify(
			repo => repo.UpdateProfileAsync(It.IsAny<Authusers>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task UpdateMyProfileAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		AuthenticateAs(userId);

		_repository
			.Setup(repo => repo.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
			.ReturnsAsync((Authusers?)null);

		var update = new UpdateUserProfileDTO
		{
			FirstName = "Jane",
			LastName = "Smith"
		};

		// Act
		Func<Task> act = async () => await CreateService().UpdateMyProfileAsync(update, CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage("The authenticated user profile was not found.");
	}
}
