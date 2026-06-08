using Auth.Data.Entities;
using Auth.DTO;
using Auth.Features.Login;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;
using static Auth.Features.LoginWeb.LoginWebHandler;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class LoginIntegrationTests : BaseIntegrationTest
{
	public LoginIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	[Fact]
	public async Task Login_ShouldReturnSuccess_WhenCredentialsAreCorrect()
	{
		// Arrange
		await SeedUserData();

		var command = new LoginCommand("john@example.com", "p@ssw0rd!");

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.loginResponseDTO.email.Should().Be("john@example.com");
		result.loginResponseDTO.userId.Should().NotBeEmpty();
		result.loginResponseDTO.name.Should().Be("Admin Admin Admin");
		result.loginResponseDTO.access_token.Should().NotBeNullOrEmpty();
		result.loginResponseDTO.token_type.Should().Be("bearer");

	}


	[Fact]
	public async Task Login_ShouldReturnNotFound_WhenCredentialsAreIncorrect()
	{
		// Arrange
		await SeedUserData();

		var command = new LoginCommand("john@example.com", "wrongpassword");

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");
	}



	[Fact]
	public async Task LoginWeb_ShouldReturnSuccess_WhenCredentialsAreCorrect()
	{
		// Arrange
		await SeedUserData();

		var command = new LoginWebCommand(new LoginWebCred("john@example.com", "p@ssw0rd!", false));

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.loginResponseWebDTO.Should().NotBeNull();
		result.loginResponseWebDTO.AccessToken.Should().NotBeNullOrEmpty();
		result.loginResponseWebDTO.UserId.Should().NotBeNullOrEmpty();
		result.loginResponseWebDTO.RefreshToken.Should().NotBeNullOrEmpty();
		result.loginResponseWebDTO.TokenType.Should().Be("bearer");
		result.loginResponseWebDTO.ExpiresIn.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task LoginWeb_ShouldUnauthorizedAccessException_WhenUserHasNoDesignatedApplication()
	{
		// Arrange
		await SeedUserOnlyData();

		var command = new LoginWebCommand(new LoginWebCred("john@example.com", "p@ssw0rd!", false));

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Your account has not been approved yet. Please contact an administrator for assistance.");
	}



	[Fact]
	public async Task LoginWeb_ShouldReturnNotFound_WhenCredentialsAreCorrect()
	{
		// Arrange
		await SeedUserData();
		var command = new LoginWebCommand(new LoginWebCred("john@example.com", "wrongpassword", false));

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");
	}

	[Fact]
	public async Task Login_ShouldThrow_AfterFourFailedAttempts()
	{
		// Arrange
		await SeedUserData();
		var userId = _dbContext.AuthUsers.First(u => u.Email == "john@example.com").Id;

		// Act & Assert - First failed attempt (attempt count = 1)
		var command1 = new LoginCommand("john@example.com", "wrongpassword1");
		Func<Task> act1 = async () => { await _sender.Send(command1); };
		await act1.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");

		// Verify attempt was recorded but not locked yet
		var attemptsAfter1 = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == userId);
		attemptsAfter1.Should().BeNull(); // Not locked after 1 attempt

		// Act & Assert - Second failed attempt (attempt count = 2)
		var command2 = new LoginCommand("john@example.com", "wrongpassword2");
		Func<Task> act2 = async () => { await _sender.Send(command2); };
		await act2.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");

		// Verify still not locked after 2 attempts
		var attemptsAfter2 = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == userId);
		attemptsAfter2.Should().BeNull(); // Not locked after 2 attempts (warning threshold)


		// Act & Assert - Second failed attempt (attempt count = 3)
		var command3 = new LoginCommand("john@example.com", "wrongpassword2");
		Func<Task> act3 = async () => { await _sender.Send(command3); };
		await act3.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");

		// Verify still not locked after 2 attempts
		var attemptsAfter3 = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == userId);
		attemptsAfter3.Should().BeNull(); // Not locked after 2 attempts (warning threshold)

		// Act & Assert - Third failed attempt (attempt count = 4) - should lock account
		var command4 = new LoginCommand("john@example.com", "wrongpassword3");
		Func<Task> act4 = async () => { await _sender.Send(command4); };
		await act3.Should().ThrowAsync<UnauthorizedAccessException>()
			.WithMessage("Too many failed login attempts. Please try again later.");

		// Verify account is now locked
		var lockedAttempts = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == userId);
		lockedAttempts.Should().NotBeNull();
		lockedAttempts!.Attempts.Should().Be(4);
	}

	[Fact]
	public async Task Login_ShouldThrow_WhenAccountIsLocked()
	{
		// Arrange
		await SeedUserDataWithLockedAccount();

		var command = new LoginCommand("locked@example.com", "p@ssw0rd!");

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>()
			.WithMessage("Too many failed login attempts. Please try again later.");
	}

	[Fact]
	public async Task LoginWeb_ShouldThrow_AfterThreeFailedAttempts()
	{
		// Arrange
		await SeedUserData();
		var userId = _dbContext.AuthUsers.First(u => u.Email == "john@example.com").Id;

		// Act & Assert - First failed attempt (attempt count = 1)
		var command1 = new LoginWebCommand(new LoginWebCred("john@example.com", "wrongpassword1", false));
		Func<Task> act1 = async () => { await _sender.Send(command1); };
		await act1.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");

		// Verify attempt was recorded but not locked yet
		var attemptsAfter1 = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == userId);
		attemptsAfter1.Should().BeNull(); // Not locked after 1 attempt

		// Act & Assert - Second failed attempt (attempt count = 2)
		var command2 = new LoginWebCommand(new LoginWebCred("john@example.com", "wrongpassword2", false));
		Func<Task> act2 = async () => { await _sender.Send(command2); };
		await act2.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");

		// Verify still not locked after 2 attempts
		var attemptsAfter2 = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == userId);
		attemptsAfter2.Should().BeNull(); // Not locked after 2 attempts (warning threshold)


		var command3 = new LoginWebCommand(new LoginWebCred("john@example.com", "wrongpassword2", false));
		Func<Task> act3 = async () => { await _sender.Send(command2); };
		await act2.Should().ThrowAsync<NotFoundException>().WithMessage("Invalid username or password.");

		// Verify still not locked after 3 attempts
		var attemptsAfter3 = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == userId);
		attemptsAfter2.Should().BeNull(); // Not locked after 3 attempts (warning threshold)


		// Act & Assert - Third failed attempt (attempt count = 4) - should lock account
		var command4 = new LoginWebCommand(new LoginWebCred("john@example.com", "wrongpassword3", false));
		Func<Task> act4 = async () => { await _sender.Send(command4); };
		await act4.Should().ThrowAsync<UnauthorizedAccessException>()
			.WithMessage("Too many failed login attempts. Please try again later.");

		// Verify account is now locked
		var lockedAttempts = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == userId);
		lockedAttempts.Should().NotBeNull();
		lockedAttempts!.Attempts.Should().Be(4);
	}

	[Fact]
	public async Task LoginWeb_ShouldThrow_WhenAccountIsLocked()
	{
		// Arrange
		await SeedUserDataWithLockedAccount();

		var command = new LoginWebCommand(new LoginWebCred("locked@example.com", "p@ssw0rd!", false));

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<UnauthorizedAccessException>()
			.WithMessage("Too many failed login attempts. Please try again later.");
	}

	[Fact]
	public async Task Login_ShouldClearAttempts_WhenLoginSuccessfulAfterFailures()
	{
		// Arrange
		await SeedUserData();
		var userId = _dbContext.AuthUsers.First(u => u.Email == "john@example.com").Id;

		// Act - First failed attempt
		var commandFail = new LoginCommand("john@example.com", "wrongpassword");
		Func<Task> actFail = async () => { await _sender.Send(commandFail); };
		await actFail.Should().ThrowAsync<NotFoundException>();

		// Act - Successful login should clear attempts
		var commandSuccess = new LoginCommand("john@example.com", "p@ssw0rd!");
		var result = await _sender.Send(commandSuccess);

		// Assert
		result.Should().NotBeNull();
		result.loginResponseDTO.email.Should().Be("john@example.com");
	}

	[Fact]
	public async Task LoginWeb_ShouldClearAttempts_WhenLoginSuccessfulAfterFailures()
	{
		// Arrange
		await SeedUserData();
		var userId = _dbContext.AuthUsers.First(u => u.Email == "john@example.com").Id;

		// Act - First failed attempt
		var commandFail = new LoginWebCommand(new LoginWebCred("john@example.com", "wrongpassword", false));
		Func<Task> actFail = async () => { await _sender.Send(commandFail); };
		await actFail.Should().ThrowAsync<NotFoundException>();

		// Act - Successful login should clear attempts
		var commandSuccess = new LoginWebCommand(new LoginWebCred("john@example.com", "p@ssw0rd!", false));
		var result = await _sender.Send(commandSuccess);

		// Assert
		result.Should().NotBeNull();
		result.loginResponseWebDTO.Should().NotBeNull();
		result.loginResponseWebDTO.UserId.Should().NotBeNullOrEmpty();
	}


	private async Task SeedUserData()
	{
		var userId = Guid.CreateVersion7();

		var user = new Authusers
		{
			Id = userId,
			Email = "john@example.com",
			PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
			FirstName = "Admin",
			MiddleName = "Admin",
			LastName = "Admin",
			IsApproved = true
		};

		var userRole = new List<AuthRole>
			{
				new AuthRole
				{
					RoleName = "SuperAdmin",
					Description = "Super Admin"
				},
				new AuthRole
				{
					RoleName = "Admin",
					Description = "Administrator Role"
				},
				new AuthRole
				{
					RoleName = "User",
					Description = "User Role"
				}
			};


		var submenu = new List<AuthSubMenu>
			{
				new AuthSubMenu
				{
					SubMenuName = "CNX Dashboard",
					Description = "List of Subjects"
				},
				new AuthSubMenu
				{
					SubMenuName = "IDV",
					Description = "Philsys IDV"
				}
			};

		var authapplication = new List<AuthApplication>
			{
				new AuthApplication
				{
					AppName = "CNX",
					Description = "Concentrix API"
				},
				new AuthApplication
				{
					AppName = "Philsys",
					Description = "IDV"
				}
			};

		var authUserRole = new List<AuthUserAppRole>
			{
				new AuthUserAppRole
				{
					UserId = userId,
					AppId = 1,
					Submenu = 1,
					RoleId = 1,
					AssignedBy = userId
				},
				new AuthUserAppRole
				{
					UserId = userId,
					AppId = 2,
					Submenu= 2,
					RoleId = 2,
					AssignedBy = userId
				}
			};

		_dbContext.AuthUsers.Add(user);
		_dbContext.AuthApplications.AddRange(authapplication);
		_dbContext.AuthRoles.AddRange(userRole);
		_dbContext.AuthSubmenu.AddRange(submenu);
		_dbContext.AuthUserAppRoles.AddRange(authUserRole);

		await _dbContext.SaveChangesAsync();
	}



	private async Task SeedUserOnlyData()
	{
		var userId = Guid.CreateVersion7();

		var user = new Authusers
		{
			Id = userId,
			Email = "john@example.com",
			PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
			FirstName = "Admin",
			MiddleName = "Admin",
			LastName = "Admin"
		};

		_dbContext.AuthUsers.Add(user);

		await _dbContext.SaveChangesAsync();
	}

	private async Task SeedUserDataWithLockedAccount()
	{
		var userId = Guid.CreateVersion7();

		var user = new Authusers
		{
			Id = userId,
			Email = "locked@example.com",
			PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
			FirstName = "Locked",
			MiddleName = "User",
			LastName = "Account",
			IsApproved = true
		};

		var lockedAttempt = new AuthAttempts
		{
			UserId = userId,
			Email = "locked@example.com",
			Attempts = 4,
			Message = "Account is locked due to too many failed attempts.",
			CreatedAt = DateTime.UtcNow.AddMinutes(-5), // Locked 5 minutes ago, still within lockout period
			LockReleaseAt = DateTime.UtcNow.AddMinutes(40)
		};

		var userRole = new List<AuthRole>
		{
			new AuthRole
			{
				RoleName = "SuperAdmin",
				Description = "Super Admin"
			}
		};

		var submenu = new List<AuthSubMenu>
		{
			new AuthSubMenu
			{
				SubMenuName = "CNX Dashboard",
				Description = "List of Subjects"
			}
		};

		var authapplication = new List<AuthApplication>
		{
			new AuthApplication
			{
				AppName = "CNX",
				Description = "Concentrix API"
			}
		};

		var authUserRole = new List<AuthUserAppRole>
		{
			new AuthUserAppRole
			{
				UserId = userId,
				AppId = 1,
				Submenu = 1,
				RoleId = 1,
				AssignedBy = userId
			}
		};

		_dbContext.AuthUsers.Add(user);
		_dbContext.AuthAttempts.Add(lockedAttempt);
		_dbContext.AuthApplications.AddRange(authapplication);
		_dbContext.AuthRoles.AddRange(userRole);
		_dbContext.AuthSubmenu.AddRange(submenu);
		_dbContext.AuthUserAppRoles.AddRange(authUserRole);

		await _dbContext.SaveChangesAsync();
	}
}
