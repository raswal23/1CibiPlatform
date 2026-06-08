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
	public async Task GetNewAccessToken_ShouldThrowNotFound_WhenUserDoesNotExist()
	{
		// Arrange
		var nonExistentUserId = Guid.CreateVersion7();

		var command = new GetNewAccessTokenCommand(nonExistentUserId);

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("Refresh Token is not found.");
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


		var command = new GetNewAccessTokenCommand(user.Id);

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
	public async Task GetNewAccessToken_ShouldThrowNotFoundException_WhenUserIsInvalid()
	{
		// Arrange
		var olduUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var differentUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
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

		var command = new GetNewAccessTokenCommand(differentUserId);

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("Refresh Token is not found.");
	}

	private static string ComputeSha256Base64(string input)
	{
		using var sha256 = SHA256.Create();
		var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
		return Convert.ToBase64String(hash);
	}
}
