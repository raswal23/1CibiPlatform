using Moq;
using FluentAssertions;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;
using Auth.DTO;
using BuildingBlocks.Exceptions;
using Auth.Data.Entities;
using Microsoft.Extensions.Caching.Hybrid;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class LoginServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;

	public LoginServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task LoginAsync_ShouldThrow_WhenUserNotFound()
	{
		// Arrange
		var service = _fixture.LoginService;
		_fixture.MockAuthRepository.Setup(x => x.GetUserDataAsync(It.IsAny<LoginWebCred>()))
		.ReturnsAsync((LoginDTO?)null);

		// Act
		Func<Task> act = async () => await service.LoginAsync("bad", "bad");

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");
	}

	[Fact]
	public async Task LoginAsync_ShouldThrow_WhenPasswordInvalid()
	{
		// Arrange
		var service = _fixture.LoginService;
		var userId = Guid.CreateVersion7();
		var loginDto = new LoginDTO(userId, "hash", "email@example.com", "F", "L", null, true, new List<int> { 1 }, new List<List<int>> { new List<int> { 1 } }, new List<int> { 1 });

		_fixture.MockAuthRepository.Setup(x => x.GetUserDataAsync(It.IsAny<LoginWebCred>())).ReturnsAsync(loginDto);
		_fixture.MockPasswordHasherService.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
		_fixture.MockAuthRepository.Setup(x => x.GetLockedUserAsync(userId)).ReturnsAsync((AuthAttempts?)null);
		_fixture.MockAuthRepository.Setup(x => x.SaveLockedUserAsync(It.IsAny<AuthAttempts>())).ReturnsAsync(false);

		// Act
		Func<Task> act = async () => await service.LoginAsync("user", "wrong");

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");
	}

	[Fact]
	public async Task LoginAsync_ShouldReturnResponse_WhenSuccessful()
	{
		// Arrange
		var service = _fixture.LoginService;
		var userId = Guid.CreateVersion7();
		var loginDto = new LoginDTO(userId, "hash", "email@example.com", "John", "Doe", null, true, new List<int> { 1 }, new List<List<int>> { new List<int> { 1 } }, new List<int> { 1 });

		_fixture.MockAuthRepository.Setup(x => x.GetUserDataAsync(It.IsAny<LoginWebCred>())).ReturnsAsync(loginDto);
		_fixture.MockPasswordHasherService.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
		_fixture.MockJwtService.Setup(x => x.GetAccessToken(It.IsAny<LoginDTO>())).Returns("token");
		_fixture.MockAuthRepository.Setup(x => x.GetLockedUserAsync(userId)).ReturnsAsync((AuthAttempts?)null);

		// Act
		var resp = await service.LoginAsync("user", "pass");

		// Assert
		resp.Should().NotBeNull();
		resp.userId.Should().NotBeNull();
		resp.name.Should().Be("John Doe");
	}

	[Fact]
	public async Task LoginAsync_ShouldThrow_WhenAccountLockedAfterFourAttempts()
	{
		// Arrange
		var service = _fixture.LoginService;
		var userId = Guid.CreateVersion7();
		var loginDto = new LoginDTO(userId, "hash", "email@example.com", "John", "Doe", null, true, new List<int> { 1 }, new List<List<int>> { new List<int> { 1 } }, new List<int> { 1 });

		// Setup a dictionary to simulate cache persistence across calls
		var cacheStore = new Dictionary<string, int>();
		var cacheKey = $"user_attempt_{userId}";

		_fixture.MockAuthRepository.Setup(x => x.GetUserDataAsync(It.IsAny<LoginWebCred>())).ReturnsAsync(loginDto);
		_fixture.MockPasswordHasherService.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

		// Setup GetLockedUserAsync to return locked user after 3 attempts
		_fixture.MockAuthRepository.Setup(x => x.GetLockedUserAsync(userId))
			.ReturnsAsync(() =>
			{
				if (cacheStore.ContainsKey(cacheKey) && cacheStore[cacheKey] >= 4)
				{
					return new AuthAttempts
					{
						UserId = userId,
						Email = "email@example.com",
						Attempts = 4,
						Message = "Account is locked due to too many failed attempts.",
						CreatedAt = DateTime.UtcNow,
						LockReleaseAt = DateTime.UtcNow.AddMinutes(40)
					};
				}
				return null;
			});

		_fixture.MockAuthRepository.Setup(x => x.SaveLockedUserAsync(It.IsAny<AuthAttempts>())).ReturnsAsync(true);

		// Setup HybridCache mock to persist values
		_fixture.MockHybridCache
			.Setup(x => x.GetOrCreateAsync<string, int>(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<Func<string, CancellationToken, ValueTask<int>>>(),
				It.IsAny<HybridCacheEntryOptions>(),
				It.IsAny<IEnumerable<string>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync((string key, string state, Func<string, CancellationToken, ValueTask<int>> factory, HybridCacheEntryOptions options, IEnumerable<string> tags, CancellationToken token) =>
			{
				if (!cacheStore.ContainsKey(key))
				{
					cacheStore[key] = 0;
				}
				return cacheStore[key];
			});

		_fixture.MockHybridCache
			.Setup(x => x.SetAsync(
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<HybridCacheEntryOptions>(),
				It.IsAny<IEnumerable<string>>(),
				It.IsAny<CancellationToken>()))
			.Returns((string key, int value, HybridCacheEntryOptions options, IEnumerable<string> tags, CancellationToken token) =>
			{
				cacheStore[key] = value;
				return ValueTask.CompletedTask;
			});

		_fixture.MockHybridCache
			.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.Returns((string key, CancellationToken token) =>
			{
				cacheStore.Remove(key);
				return ValueTask.CompletedTask;
			});

		// Act & Assert
		// First attempt (attempt = 1)
		await Assert.ThrowsAsync<NotFoundException>(() => service.LoginAsync("user", "wrong1"));
		cacheStore[cacheKey].Should().Be(1);

		// Second attempt (attempt = 2)
		await Assert.ThrowsAsync<NotFoundException>(() => service.LoginAsync("user", "wrong2"));
		cacheStore[cacheKey].Should().Be(2);


		// Second attempt (attempt = 4)
		await Assert.ThrowsAsync<NotFoundException>(() => service.LoginAsync("user", "wrong3"));
		cacheStore[cacheKey].Should().Be(3);

		// Fourth attempt (attempt = 4, account gets locked)
		var act = async () => await service.LoginAsync("user", "wrong4");
		await act.Should().ThrowAsync<UnauthorizedAccessException>()
			.WithMessage("Too many failed login attempts. Please try again later.");
	}

	[Fact]
	public async Task LoginAsync_ShouldThrow_WhenAccountAlreadyLocked()
	{
		// Arrange
		var service = _fixture.LoginService;
		var userId = Guid.CreateVersion7();
		var loginDto = new LoginDTO(userId, "hash", "email@example.com", "John", "Doe", null, true, new List<int> { 1 }, new List<List<int>> { new List<int> { 1 } }, new List<int> { 1 });
		var lockedUser = new AuthAttempts
		{
			UserId = userId,
			Email = "email@example.com",
			Attempts = 4,
			Message = "Account is locked due to too many failed attempts.",
			CreatedAt = DateTime.UtcNow.AddMinutes(-5), // Locked 5 minutes ago
			LockReleaseAt = DateTime.UtcNow.AddMinutes(40)
		};

		_fixture.MockAuthRepository.Setup(x => x.GetUserDataAsync(It.IsAny<LoginWebCred>())).ReturnsAsync(loginDto);
		_fixture.MockPasswordHasherService.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
		_fixture.MockAuthRepository.Setup(x => x.GetLockedUserAsync(userId)).ReturnsAsync(lockedUser);

		// Act
		Func<Task> act = async () => await service.LoginAsync("user", "wrong");

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>()
			.WithMessage("Too many failed login attempts. Please try again later.");
	}

	[Fact]
	public async Task LoginAsync_ShouldAllowLogin_WhenLockoutExpired()
	{
		// Arrange
		var service = _fixture.LoginService;
		var userId = Guid.CreateVersion7();
		var loginDto = new LoginDTO(userId, "hash", "email@example.com", "John", "Doe", null, true, new List<int> { 1 }, new List<List<int>> { new List<int> { 1 } }, new List<int> { 1 });
		var expiredLockedUser = new AuthAttempts
		{
			UserId = userId,
			Email = "email@example.com",
			Attempts = 3,
			Message = "Account is locked due to too many failed attempts.",
			CreatedAt = DateTime.UtcNow.AddMinutes(-70) // Locked 20 minutes ago (expired)
		};

		_fixture.MockAuthRepository.Setup(x => x.GetUserDataAsync(It.IsAny<LoginWebCred>())).ReturnsAsync(loginDto);
		_fixture.MockPasswordHasherService.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
		_fixture.MockJwtService.Setup(x => x.GetAccessToken(It.IsAny<LoginDTO>())).Returns("token");
		_fixture.MockAuthRepository.Setup(x => x.GetLockedUserAsync(userId)).ReturnsAsync(expiredLockedUser);
		_fixture.MockAuthRepository.Setup(x => x.DeleteLockedUserAsync(expiredLockedUser)).ReturnsAsync(true);

		// Act
		var resp = await service.LoginAsync("user", "pass");

		// Assert
		resp.Should().NotBeNull();
		resp.userId.Should().NotBeNull();
		resp.name.Should().Be("John Doe");
		_fixture.MockAuthRepository.Verify(x => x.DeleteLockedUserAsync(expiredLockedUser), Times.Once);
	}

	[Fact]
	public async Task LoginAsync_ShouldThrow_WhenUserNotApproved()
	{
		// Arrange
		var service = _fixture.LoginService;
		var userId = Guid.CreateVersion7();
		var loginDto = new LoginDTO(userId, "hash", "email@example.com", "John", "Doe", null, false, new List<int> { 1 }, new List<List<int>> { new List<int> { 1 } }, new List<int> { 1 });

		_fixture.MockAuthRepository.Setup(x => x.GetUserDataAsync(It.IsAny<LoginWebCred>())).ReturnsAsync(loginDto);
		_fixture.MockPasswordHasherService.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
		_fixture.MockAuthRepository.Setup(x => x.GetLockedUserAsync(userId)).ReturnsAsync((AuthAttempts?)null);

		// Act
		Func<Task> act = async () => await service.LoginAsync("user", "pass");

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>()
			.WithMessage("Your account has not been approved yet. Please contact an administrator for assistance.");
	}
}
