using Auth.DTO;
using Auth.Features.IsChangePasswordTokenValid;
using Auth.Features.UpdatePassword;
using Auth.Data.Entities;
using FluentAssertions;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class PasswordTokenIntegrationTests : BaseIntegrationTest
{
	public PasswordTokenIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
	}

	[Fact]
	public async Task IsChangePasswordTokenValid_ShouldReturnTrue_WhenTokenIsValid()
	{
		// Arrange
		var user = await SeedUserData();

		var tokenHash = Guid.NewGuid().ToString();

		var passwordToken = new PasswordResetToken
		{
			UserId = user.Id,
			TokenHash = tokenHash,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddMinutes(30),
			IsUsed = false
		};

		_dbContext.PasswordResetToken.Add(passwordToken);
		await _dbContext.SaveChangesAsync();

		var requestDto = new ForgotPasswordTokenRequestDTO(user.Id, tokenHash);

		var command = new IsChangePasswordTokenValidCommand(requestDto);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public async Task UpdatePassword_ShouldUpdatePassword_WhenTokenIsValid()
	{
		// Arrange
		var user = await SeedUserData();

		var tokenHash = Guid.NewGuid().ToString();

		var passwordToken = new PasswordResetToken
		{
			UserId = user.Id,
			TokenHash = tokenHash,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddMinutes(30),
			IsUsed = false
		};

		_dbContext.PasswordResetToken.Add(passwordToken);
		await _dbContext.SaveChangesAsync();

		var newPassword = "NewP@ssw0rd!";

		var requestDto = new UpdatePasswordRequestDTO(tokenHash, newPassword);

		var command = new UpdatePasswordCommand(requestDto);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.IsSuccessful.Should().BeTrue();

		// verify password was actually updated in DB
		//var updatedUser = await _dbContext.AuthUsers.FindAsync(user.Id);
		//updatedUser.Should().NotBeNull();
		//_passwordHasherService.VerifyPassword(updatedUser!.PasswordHash, newPassword).Should().BeTrue();
	}

	[Fact]
	public async Task UpdatePassword_ShouldThrowUnauthorized_WhenTokenNotPresent()
	{
		// Arrange - no token stored
		var user = await SeedUserData();
		var tokenHash = "non-existent-token";
		var requestDto = new UpdatePasswordRequestDTO(tokenHash, "NewPass1!");
		var command = new UpdatePasswordCommand(requestDto);

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid or expired token.");
	}

	[Fact]
	public async Task UpdatePassword_ShouldThrowUnauthorized_WhenTokenExpired()
	{
		// Arrange - create expired token
		var user = await SeedUserData();
		var tokenHash = Guid.NewGuid().ToString();
		var passwordToken = new PasswordResetToken
		{
			UserId = user.Id,
			TokenHash = tokenHash,
			CreatedAt = DateTime.UtcNow.AddHours(-2),
			ExpiresAt = DateTime.UtcNow.AddHours(-1), // expired
			IsUsed = false
		};
		_dbContext.PasswordResetToken.Add(passwordToken);
		await _dbContext.SaveChangesAsync();

		var requestDto = new UpdatePasswordRequestDTO(tokenHash, "NewPass1!");
		var command = new UpdatePasswordCommand(requestDto);

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Invalid or expired token.");
	}


	private async Task<Authusers> SeedUserData()
	{
		var user = new Authusers
		{
			Id = Guid.NewGuid(),
			Email = "jane2@example.com",
			PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
			FirstName = "Test",
			LastName = "User"
		};
		_dbContext.AuthUsers.Add(user);
		await _dbContext.SaveChangesAsync();
		return user;
	}
}
