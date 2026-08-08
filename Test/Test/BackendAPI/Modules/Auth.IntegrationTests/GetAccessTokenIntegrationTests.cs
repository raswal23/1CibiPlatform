using System.Security.Cryptography;
using System.Text;
using Auth.Features.GetNewAccessToken;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Auth.Data.Entities;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class GetAccessTokenIntegrationTests : BaseIntegrationTest
{
	public GetAccessTokenIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
	}

	[Fact]
	public async Task GetNewAccessToken_ShouldThrowUnauthorized_WhenRefreshCookieIsMissing()
	{
		var command = new GetNewAccessTokenCommand();

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid refresh token.");
	}

	[Fact]
	public async Task GetNewAccessToken_ShouldReturnOk_WhenRefreshTokenIsValid()
	{
		// Arrange - seed user and refresh token in DB
		var user = new Authusers
		{
			Id = Guid.CreateVersion7(),
			Email = "refreshuser@example.com",
			PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
			FirstName = "Refresh",
			LastName = "User",
			IsActive = true
		};
		_dbContext.AuthUsers.Add(user);

		var refreshToken = "valid-refresh-token";
		var hashed = ComputeSha256Base64(refreshToken);

		var authRefresh = new AuthRefreshToken
		{
			UserId = user.Id,
			TokenHash = hashed,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(7),
			IsActive = true
		};

		_dbContext.AuthRefreshToken.Add(authRefresh);
		await _dbContext.SaveChangesAsync();


		var refreshCookieName = _configuration["AuthWeb:AuthWebHttpCookieOnlyKey"]!;
		_httpContextAccessor.HttpContext!.Request.Headers.Cookie = $"{refreshCookieName}={refreshToken}";
		var command = new GetNewAccessTokenCommand();

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.loginResponseWebDTO.Should().NotBeNull();
		result.loginResponseWebDTO.AccessToken.Should().NotBeNullOrEmpty();
		result.loginResponseWebDTO.RefreshToken.Should().NotBeNullOrEmpty();
		result.loginResponseWebDTO.TokenType.Should().Be("bearer");
		result.loginResponseWebDTO.UserId.Should().Be(user.Id.ToString());
		result.loginResponseWebDTO.ExpiresIn.Should().BeGreaterThan(0);
	}


	[Fact]
	public async Task GetNewAccessToken_ShouldThrowUnauthorized_WhenRefreshCookieIsInvalid()
	{
		// Arrange
		var olduUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var user = new Authusers
		{
			Id = olduUserId,
			Email = "refreshuser2@example.com",
			PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
			FirstName = "Refresh",
			LastName = "User",
			IsActive = true
		};
		_dbContext.AuthUsers.Add(user);

		var realToken = "real-refresh-token";
		var hashed = ComputeSha256Base64(realToken);

		var authRefresh = new AuthRefreshToken
		{
			UserId = olduUserId,
			TokenHash = hashed,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(7),
			IsActive = true
		};

		_dbContext.AuthRefreshToken.Add(authRefresh);
		await _dbContext.SaveChangesAsync();

		var refreshCookieName = _configuration["AuthWeb:AuthWebHttpCookieOnlyKey"]!;
		_httpContextAccessor.HttpContext!.Request.Headers.Cookie = $"{refreshCookieName}=invalid-refresh-token";
		var command = new GetNewAccessTokenCommand();

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid refresh token.");
	}

	private static string ComputeSha256Base64(string input)
	{
		using var sha256 = SHA256.Create();
		var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
		return Convert.ToBase64String(hash);
	}
}
