using System.Security.Claims;
using Auth.Constants;
using Auth.Data.Entities;
using Auth.DTO;
using Auth.Features.UserProfile.Command.UpdateMyProfile;
using Auth.Features.UserProfile.Query.GetMyProfile;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class UserProfileIntegrationTests : BaseIntegrationTest
{
	public UserProfileIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	[Fact]
	public async Task GetMyProfile_ShouldReturnTheAuthenticatedUsersOwnProfile()
	{
		// Arrange
		var user = await SeedUserAsync("profile-read@example.com", "Read", "Quincy", "User");
		AuthenticateAs(user.Id);

		// Act
		var result = await _sender.Send(new GetMyProfileQueryRequest());

		// Assert
		result.Profile.UserId.Should().Be(user.Id);
		result.Profile.Email.Should().Be("profile-read@example.com");
		result.Profile.FirstName.Should().Be("Read");
		result.Profile.MiddleName.Should().Be("Quincy");
		result.Profile.LastName.Should().Be("User");
		result.Profile.FullName.Should().Be("Read Quincy User");
	}

	[Fact]
	public async Task GetMyProfile_ShouldThrowUnauthorized_WhenNoPrincipalIsPresent()
	{
		// Arrange
		ClearAuthentication();

		// Act
		Func<Task> act = async () => await _sender.Send(new GetMyProfileQueryRequest());

		// Assert
		await act.Should().ThrowAsync<UnauthorizedException>();
	}

	[Fact]
	public async Task GetMyProfile_ShouldThrowNotFound_WhenTheUserRowIsMissing()
	{
		// Arrange
		AuthenticateAs(Guid.CreateVersion7());

		// Act
		Func<Task> act = async () => await _sender.Send(new GetMyProfileQueryRequest());

		// Assert
		await act.Should().ThrowAsync<NotFoundException>();
	}

	[Fact]
	public async Task UpdateMyProfile_ShouldPersistNameChanges()
	{
		// Arrange
		var user = await SeedUserAsync("profile-update@example.com", "Old", "Middle", "Name");
		AuthenticateAs(user.Id);

		var command = new UpdateMyProfileCommand(new UpdateUserProfileDTO
		{
			FirstName = "New",
			MiddleName = "Edited",
			LastName = "Surname"
		});

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Profile.FirstName.Should().Be("New");
		result.Profile.MiddleName.Should().Be("Edited");
		result.Profile.LastName.Should().Be("Surname");
		result.Profile.FullName.Should().Be("New Edited Surname");

		var persisted = await _dbContext.AuthUsers
			.AsNoTracking()
			.SingleAsync(item => item.Id == user.Id);

		persisted.FirstName.Should().Be("New");
		persisted.MiddleName.Should().Be("Edited");
		persisted.LastName.Should().Be("Surname");
		persisted.Email.Should().Be("profile-update@example.com");
	}

	[Fact]
	public async Task UpdateMyProfile_ShouldClearMiddleName_WhenSubmittedBlank()
	{
		// Arrange
		var user = await SeedUserAsync("profile-middle@example.com", "Has", "Middle", "Name");
		AuthenticateAs(user.Id);

		var command = new UpdateMyProfileCommand(new UpdateUserProfileDTO
		{
			FirstName = "Has",
			MiddleName = "",
			LastName = "Name"
		});

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Profile.MiddleName.Should().BeNull();

		var persisted = await _dbContext.AuthUsers
			.AsNoTracking()
			.SingleAsync(item => item.Id == user.Id);

		persisted.MiddleName.Should().BeNull();
	}

	[Fact]
	public async Task UpdateMyProfile_ShouldNotAffectOtherUsers()
	{
		// Arrange
		var caller = await SeedUserAsync("profile-caller@example.com", "Caller", null, "One");
		var bystander = await SeedUserAsync("profile-bystander@example.com", "By", null, "Stander");
		AuthenticateAs(caller.Id);

		var command = new UpdateMyProfileCommand(new UpdateUserProfileDTO
		{
			FirstName = "Renamed",
			LastName = "Caller"
		});

		// Act
		await _sender.Send(command);

		// Assert
		var persistedBystander = await _dbContext.AuthUsers
			.AsNoTracking()
			.SingleAsync(item => item.Id == bystander.Id);

		persistedBystander.FirstName.Should().Be("By");
		persistedBystander.LastName.Should().Be("Stander");
	}

	[Fact]
	public async Task UpdateMyProfile_ShouldThrowValidationException_WhenFirstNameIsMissing()
	{
		// Arrange
		var user = await SeedUserAsync("profile-invalid@example.com", "Valid", null, "User");
		AuthenticateAs(user.Id);

		var command = new UpdateMyProfileCommand(new UpdateUserProfileDTO
		{
			FirstName = "  ",
			LastName = "User"
		});

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<FluentValidation.ValidationException>();
	}

	[Fact]
	public async Task UpdateMyProfile_ShouldThrowUnauthorized_WhenNoPrincipalIsPresent()
	{
		// Arrange
		ClearAuthentication();

		var command = new UpdateMyProfileCommand(new UpdateUserProfileDTO
		{
			FirstName = "Nobody",
			LastName = "Home"
		});

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<UnauthorizedException>();
	}

	// CurrentUser reads the principal off the shared scoped HttpContext, so setting
	// it here is what makes the request "authenticated" for the handler under test.
	private void AuthenticateAs(Guid userId)
	{
		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity(
				[
					new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
					new Claim(AuthClaimTypes.UserId, userId.ToString())
				],
				"TestAuth"));
	}

	private void ClearAuthentication() =>
		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

	private async Task<Authusers> SeedUserAsync(
		string email,
		string firstName,
		string? middleName,
		string lastName)
	{
		var user = new Authusers
		{
			Id = Guid.CreateVersion7(),
			Email = email,
			PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
			FirstName = firstName,
			MiddleName = middleName,
			LastName = lastName,
			IsActive = true,
			IsApproved = true
		};

		_dbContext.AuthUsers.Add(user);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		return user;
	}
}
