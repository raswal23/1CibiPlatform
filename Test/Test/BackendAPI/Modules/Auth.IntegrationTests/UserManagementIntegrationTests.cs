using Auth.Data.Entities;
using Auth.DTO;
using Auth.Features.AccountApprovalNotification;
using Auth.Features.UserManagement.Command.EditUser;
using Auth.Features.UserManagement.Query.GetUnApprovedUsers;
using Auth.Features.UserManagement.Query.GetUsers;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class UserManagementIntegrationTests : BaseIntegrationTest
{
	public UserManagementIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	[Fact]
	public async Task GetUsers_ShouldReturnPaginatedUsersList()
	{
		// Arrange
		await SeedUserData();

		var query = new GetUsersQueryRequest(PageNumber: 1, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.Users.Data.Count().Should().Be(2);
	}

	[Fact]
	public async Task GetUsers_ShouldReturnUserList_BasedOnSearchTerm()
	{
		// Arrange
		await SeedUserData();

		var query = new GetUsersQueryRequest(PageNumber: 1, PageSize: 1, SearchTerm: "Admin4");

		// Act
		var result = await _sender.Send(query);
		// Assert
		result.Should().NotBeNull();

		result.Users.Count.Should().Be(1);
		result.Users.Data.ElementAt(0).firstName.Should().Be("Admin4");
		result.Users.Data.ElementAt(0).email.Should().Be("john@example4.com");
	}

	[Fact]
	public async Task GetUsers_ShouldReturnEmptyList_WhenNoUsersExist()
	{
		// Arrange
		var query = new GetUsersQueryRequest(PageNumber: 1, PageSize: 5);
		// Act
		var result = await _sender.Send(query);
		// Assert
		result.Should().NotBeNull();
		result.Users.Count.Should().Be(0);
	}

	[Fact]
	public async Task GetUsers_ShouldReturnCorrectPage_WhenPageNumberAndSizeAreSpecified()
	{
		// Arrange
		await SeedUserData();
		var query = new GetUsersQueryRequest(PageNumber: 1, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Users.PageIndex.Should().Be(1);
		result.Users.PageSize.Should().Be(2);
	}

	[Fact]
	public async Task GetUsers_ShouldReturnEmptyList_WhenPageNumberExceedsTotalPages()
	{
		// Arrange
		await SeedUserData();

		var query = new GetUsersQueryRequest(PageNumber: 3, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Users.Data.Count().Should().Be(0);
	}

	[Fact]
	public async Task GetUnApprovedUsers_ShouldReturnPaginatedUsersList()
	{
		// Arrange
		await SeedUserData();

		var query = new GetUnApprovedUsersQueryRequest(PageNumber: 1, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.Users.Data.Count().Should().Be(2);
	}

	[Fact]
	public async Task GetUnApprovedUsers_ShouldReturnUserList_BasedOnSearchTerm()
	{
		// Arrange
		await SeedUserData();

		var query = new GetUnApprovedUsersQueryRequest(PageNumber: 1, PageSize: 1, SearchTerm: "john@example1.com");

		// Act
		var result = await _sender.Send(query);
		// Assert
		result.Should().NotBeNull();

		result.Users.Count.Should().Be(1);
		result.Users.Data.ElementAt(0).firstName.Should().Be("Admin1");
		result.Users.Data.ElementAt(0).email.Should().Be("john@example1.com");
	}

	[Fact]
	public async Task GetUnApprovedUsers_ShouldReturnEmptyList_WhenNoUnApprovedUsersExist()
	{
		// Arrange
		var query = new GetUnApprovedUsersQueryRequest(PageNumber: 1, PageSize: 5);
		// Act
		var result = await _sender.Send(query);
		// Assert
		result.Should().NotBeNull();
		result.Users.Count.Should().Be(0);
	}

	[Fact]
	public async Task GetUnApprovedUserss_ShouldReturnCorrectPage_WhenPageNumberAndSizeAreSpecified()
	{
		// Arrange
		await SeedUserData();
		var query = new GetUnApprovedUsersQueryRequest(PageNumber: 1, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Users.PageIndex.Should().Be(1);
		result.Users.PageSize.Should().Be(2);
	}

	[Fact]
	public async Task GetUnApprovedUsers_ShouldReturnEmptyList_WhenPageNumberExceedsTotalPages()
	{
		// Arrange
		await SeedUserData();

		var query = new GetUnApprovedUsersQueryRequest(PageNumber: 3, PageSize: 2);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Users.Data.Count().Should().Be(0);
	}

	[Fact]
	public async Task EditUser_ShouldUpdateExistingUserSuccessfully()
	{
		// Arrange
		await SeedUserData();

		var existingUser = await _dbContext.AuthUsers
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Email == "john@example1.com");

		var user = new EditUserDTO
		{
			Email = existingUser!.Email,
			IsApproved = true
		};

		var command = new EditUserCommand(user);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result!.user.IsApproved.Should().BeTrue();
	}

	[Fact]
	public async Task EditUser_ShouldThrow_WhenUserDoesNotExist()
	{
		// Arrange
		var user = new EditUserDTO
		{
			Email = "john@example0.com",
			IsApproved = true
		};
		var command = new EditUserCommand(user);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage($"{user.Email} was not found."); ;
	}

	[Fact]
	public async Task SendToUserEmailAsync_ShouldReturnAccountApprovalNotificationResponse_WhenSuccessful()
	{
		// Arrange
		var request = new
		{
			Gmail = "john@example1.com"
		};

		var command = new AccountApprovalNotificationCommand(request.Gmail);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.IsSent.Should().BeTrue();
	}

	private async Task SeedUserData()
	{
		var users = new List<Authusers>
		{
			new Authusers
			{
				Id = Guid.CreateVersion7(),
				Email = "john@example1.com",
				PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
				FirstName = "Admin1",
				LastName = "",
				IsApproved = false
			},
			new Authusers
			{
				Id = Guid.CreateVersion7(),
				Email = "john@example2.com",
				PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
				FirstName = "Admin2",
				LastName = "",
				IsApproved = false
			},
			new Authusers
			{
				Id = Guid.CreateVersion7(),
				Email = "john@example3.com",
				PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
				FirstName = "Admin3",
				LastName = "",
				IsApproved = true
			},
			new Authusers
			{
				Id = Guid.CreateVersion7(),
				Email = "john@example4.com",
				PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
				FirstName = "Admin4",
				LastName = "",
				IsApproved = true
			},

		};
		_dbContext.AuthUsers.AddRange(users);
		await _dbContext.SaveChangesAsync();
	}
}
