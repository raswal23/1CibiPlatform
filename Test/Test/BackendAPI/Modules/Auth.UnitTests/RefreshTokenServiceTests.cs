using FluentAssertions;
using System.Net;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;
using Moq;
using Auth.DTO;
using BuildingBlocks.Exceptions;
using Auth.Data.Entities;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class RefreshTokenServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;

	public RefreshTokenServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public void GenerateRefreshToken_ShouldReturnTokenAndHash_And_ValidateHashToken_ShouldReturnTrue()
	{
		// Arrange
		var service = _fixture.RefreshTokenService;

		// Act
		var (token, hash) = service.GenerateRefreshToken();

		// Assert
		token.Should().NotBeNullOrWhiteSpace();
		hash.Should().NotBeNullOrWhiteSpace();

		// Validate hash - service expects url-decoded token when validating, so encode first (service decodes)
		var encodedToken = WebUtility.UrlEncode(token);
		var isValid = service.ValidateHashToken(encodedToken, hash);
		isValid.Should().BeTrue();
	}

	[Fact]
	public void HashToken_ShouldReturnDeterministicBase64()
	{
		var service = _fixture.RefreshTokenService;

		var token = "sometoken";
		var hash1 = service.HashToken(token);
		var hash2 = service.HashToken(token);

		hash1.Should().NotBeNullOrWhiteSpace();
		hash1.Should().Be(hash2);
	}

	[Fact]
	public async Task GetNewAccessTokenAsync_ShouldThrow_WhenCookieIsMissing()
	{
		// Arrange
		var service = _fixture.RefreshTokenService;
		// Act
		Func<Task> act = async () => await service.GetNewAccessTokenAsync();

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid refresh token.");
	}

	[Fact]
	public async Task GetNewAccessTokenAsync_ShouldThrow_WhenInvalidRefreshToken()
	{
		// Arrange
		var service = _fixture.RefreshTokenService;
		var suppliedHash = service.HashToken("sampletoken");
		_fixture.MockAuthRepository.Setup(x => x.FindActiveRefreshTokenByHashAsync(suppliedHash))
			.ReturnsAsync((AuthRefreshToken?)null);

		var context = _fixture.MockHttpContextAccessor.Object.HttpContext!;
		context.Request.Headers["Cookie"] = $"refreshKey=sampletoken";


		// Act
		Func<Task> act = async () => await service.GetNewAccessTokenAsync();

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid refresh token.");
	}

	[Fact]
	public async Task GetNewAccessTokenAsync_ShouldReturnResponse_WhenSuccessful()
	{
		// Arrange
		var service = _fixture.RefreshTokenService;
		var userId = Guid.CreateVersion7();
		var refreshToken = "refreshtoken";
		var storedHash = service.HashToken(refreshToken);
		var userData = new UserDataDTO(userId, "pw", "email@example.com", "F", "L", null, storedHash, new List<int> { 1 }, new List<List<int>> { new List<int> { 1 } }, new List<int> { 1 });
		var authRefreshToken = new AuthRefreshToken
		{
			Id = 1,
			UserId = userId,
			TokenHash = storedHash,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(7),
			IsActive = true
		};


		_fixture.MockAuthRepository.Setup(x => x.GetNewUserDataAsync(userId)).ReturnsAsync(userData);
		_fixture.MockAuthRepository.Setup(x => x.FindActiveRefreshTokenByHashAsync(storedHash)).ReturnsAsync(authRefreshToken);
		_fixture.MockAuthRepository.Setup(x => x.RotateRefreshTokenAsync(
			1, storedHash, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
		_fixture.MockJwtService.Setup(x => x.GetAccessToken(It.IsAny<LoginDTO>(), 1)).Returns("token");

		// set cookie present to simulate reuse by adding Cookie header
		var context = _fixture.MockHttpContextAccessor.Object.HttpContext!;
		context.Request.Headers["Cookie"] = $"refreshKey={refreshToken}";

		// Act
		var result = await service.GetNewAccessTokenAsync();

		// Assert
		result.Should().NotBeNull();
		result.AccessToken.Should().BeEmpty("browser tokens are delivered only through HttpOnly cookies");
		result.RefreshToken.Should().BeEmpty("browser tokens are delivered only through HttpOnly cookies");
		result.TokenType.Should().Be("bearer");
		_fixture.MockAuthRepository.Verify(x => x.RotateRefreshTokenAsync(
			1, storedHash, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
		_fixture.MockJwtService.Verify(x => x.GetAccessToken(It.IsAny<LoginDTO>(), 1), Times.Once);
		_fixture.MockAuthSessionValidator.Verify(x => x.InvalidateAsync(1, It.IsAny<CancellationToken>()), Times.Once);
	}



	[Fact]
	public async Task GetNewAccessTokenAsync_ShouldThrowUnauthorized_WhenAtomicRotationLosesRace()
	{
		// Arrange
		var service = _fixture.RefreshTokenService;
		var userId = Guid.CreateVersion7();
		var refreshToken = "refreshtoken";
		var storedHash = service.HashToken(refreshToken);
		var userData = new UserDataDTO(userId, "pw", "email@example.com", "F", "L", null, storedHash, new List<int> { 1 }, new List<List<int>> { new List<int> { 1 } }, new List<int> { 1 });
		var authRefreshToken = new AuthRefreshToken
		{
			Id = 1,
			UserId = userId,
			TokenHash = storedHash,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(7),
			IsActive = true
		};

		_fixture.MockAuthRepository.Setup(x => x.GetNewUserDataAsync(userId)).ReturnsAsync(userData);
		_fixture.MockAuthRepository.Setup(x => x.FindActiveRefreshTokenByHashAsync(storedHash)).ReturnsAsync(authRefreshToken);
		_fixture.MockAuthRepository.Setup(x => x.RotateRefreshTokenAsync(
			1, storedHash, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
		_fixture.MockJwtService.Setup(x => x.GetAccessToken(It.IsAny<LoginDTO>(), 1)).Returns("token");

		// set cookie present to simulate reuse by adding Cookie header
		var context = _fixture.MockHttpContextAccessor.Object.HttpContext!;
		context.Request.Headers["Cookie"] = $"refreshKey={refreshToken}";

		// Act
		Func<Task> act = async () => await service.GetNewAccessTokenAsync();

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>()
			.WithMessage("Refresh token was already used or is no longer active.");
	}



	[Fact]
	public void ValidateHashToken_ShouldReturnTrue_ForUrlEncodedProvidedToken()
	{
		// Arrange
		var service = _fixture.RefreshTokenService;
		var token = "token+with/special=";
		var hash = service.HashToken(token);
		var encoded = WebUtility.UrlEncode(token);

		// Act
		var isValid = service.ValidateHashToken(encoded, hash);

		// Assert
		isValid.Should().BeTrue();
	}
}
