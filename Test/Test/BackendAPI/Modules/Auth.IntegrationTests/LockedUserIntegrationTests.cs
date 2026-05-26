using Auth.Data.Entities;
using Auth.Features.UserManagement.Command.DeleteLockedUser;
using Auth.Features.UserManagement.Query.GetLockedUsers;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class LockedUserIntegrationTests : BaseIntegrationTest
{
	public LockedUserIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	[Fact]
	public async Task GetLockedUsers_ShouldReturnLockedUsersList()
	{
		// Arrange
		await SeedLockedUsers();

		var query = new GetLockedUsersQueryRequest(PageNumber: 1, PageSize: 10);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.LockedUsers.Should().NotBeNull();
		result.LockedUsers.Data.Should().NotBeNull();
		result.LockedUsers.Data.Count().Should().Be(3);
	}

	[Fact]
	public async Task GetLockedUsers_ShouldReturnLockedUserList_BasedOnSearchTerm()
	{
		// Arrange
		await SeedLockedUsers();

		var query = new GetLockedUsersQueryRequest(PageNumber: 1, PageSize: 1, SearchTerm: "a@example.com");

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();

		result.LockedUsers.Count.Should().Be(1);
		result.LockedUsers.Data.ElementAt(0).Email.Should().Be("a@example.com");
	}

	[Fact]
	public async Task DeleteLockedUser_ShouldRemoveLockedUserSuccessfully()
	{
		// Arrange
		var id = Guid.NewGuid();
		var locked = new AuthAttempts
		{
			UserId = id,
			Email = "locked@example.com",
			Attempts = 4,
			Message = "locked",
			CreatedAt = DateTime.UtcNow
		};
		_dbContext.AuthAttempts.Add(locked);
		await _dbContext.SaveChangesAsync();

		var command = new DeleteLockedUserCommand(id);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.IsDeleted.Should().BeTrue();

		var exists = await _dbContext.AuthAttempts.FirstOrDefaultAsync(a => a.UserId == id);
		exists.Should().BeNull();
	}

	[Fact]
	public async Task DeleteLockedUser_ShouldThrow_WhenLockedUserDoesNotExist()
	{
		// Arrange
		var id = Guid.NewGuid();
		var command = new DeleteLockedUserCommand(id);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"Locked user with ID {id} was not found.");
	}

	private async Task SeedLockedUsers()
	{
		var attempts = new List<AuthAttempts>
		{
			new AuthAttempts { UserId = Guid.NewGuid(), Message = "Account is locked due to too many failed attempts.", Email = "a@example.com", Attempts = 4, CreatedAt = DateTime.UtcNow, LockReleaseAt = DateTime.UtcNow.AddMinutes(40) },
			new AuthAttempts { UserId = Guid.NewGuid(), Message = "Account is locked due to too many failed attempts.", Email = "b@example.com", Attempts = 4, CreatedAt = DateTime.UtcNow, LockReleaseAt = DateTime.UtcNow.AddMinutes(30) },
			new AuthAttempts { UserId = Guid.NewGuid(), Message = "Account is locked due to too many failed attempts.", Email = "c@example.com", Attempts = 5, CreatedAt = DateTime.UtcNow, LockReleaseAt = DateTime.UtcNow.AddMinutes(20) }
		};

		_dbContext.AuthAttempts.AddRange(attempts);
		await _dbContext.SaveChangesAsync();
	}
}