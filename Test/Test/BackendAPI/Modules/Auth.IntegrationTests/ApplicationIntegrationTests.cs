using Auth.Data.Entities;
using Auth.DTO;
using Auth.Features.UserManagement.Command.AddApplication;
using Auth.Features.UserManagement.Command.DeleteApplication;
using Auth.Features.UserManagement.Command.EditApplication;
using Auth.Features.UserManagement.Query.GetApplications;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class ApplicationIntegrationTests : BaseIntegrationTest
{
	public ApplicationIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	[Fact]
	public async Task GetApplications_ShouldReturnPaginatedApplicationsList()
	{
		// Arrange
		await SeedApplicationData();

		var query = new GetApplicationsQueryRequest(Cursor: null, PageSize: 3);

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.Applications.Items.Count.Should().Be(3);
	}

	[Fact]
	public async Task GetApplications_ShouldReturnApplicationList_BasedOnSearchTerm()
	{
		// Arrange
		await SeedApplicationData();

		var query = new GetApplicationsQueryRequest(Cursor: null, PageSize: 1, SearchTerm: "CNX");

		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();

		result.Applications.TotalCount.Should().Be(1);
		result.Applications.Items.ElementAt(0).applicationName.Should().Be("CNX");
		result.Applications.Items.ElementAt(0).Description.Should().Be("CNX Dashboard");
	}

	[Fact]
	public async Task GetApplications_ShouldReturnEmptyList_WhenNoApplicationsExist()
	{
		// Arrange
		var query = new GetApplicationsQueryRequest(Cursor: null, PageSize: 5);
		// Act
		var result = await _sender.Send(query);
		// Assert
		result.Should().NotBeNull();
		result.Applications.TotalCount.Should().Be(0);
	}

	[Fact]
	public async Task GetApplications_ShouldReturnCorrectPage_WhenCursorAndSizeAreSpecified()
	{
		// Arrange
		await SeedApplicationData();
		var query = new GetApplicationsQueryRequest(Cursor: null, PageSize: 2);

		// Act
		var page1 = await _sender.Send(query);
		page1.Applications.NextCursor.Should().NotBeNull();

		var page2 = await _sender.Send(query with { Cursor = page1.Applications.NextCursor });

		// Assert
		page1.Applications.Items.Count.Should().Be(2);
		page1.Applications.TotalCount.Should().Be(3);
		page2.Applications.TotalCount.Should().BeNull();
		page2.Applications.Items.Select(a => a.applicationId)
			.Should().NotIntersectWith(page1.Applications.Items.Select(a => a.applicationId));
	}

	[Fact]
	public async Task GetApplications_ShouldReturnNullNextCursor_WhenDataIsExhausted()
	{
		// Arrange
		await SeedApplicationData();

		var query = new GetApplicationsQueryRequest(Cursor: null, PageSize: 2);

		// Act
		var page1 = await _sender.Send(query);
		var page2 = await _sender.Send(query with { Cursor = page1.Applications.NextCursor });

		// Assert
		page2.Applications.Items.Count.Should().Be(1);
		page2.Applications.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task AddApplication_ShouldAddNewApplicationSuccessfully()
	{
		var application = new AddApplicationDTO {
			AppName = "NewApp",
			Description = "New Application",
			IsActive = true
		};
		// Arrange
		var command = new AddApplicationCommand(application);

		// Act
		var result = await _sender.Send(command);

		// Assert
	
		result.Should().NotBeNull();
		result.isAdded.Should().BeTrue();
	}

	[Fact]
	public async Task EditApplication_ShouldUpdateExistingApplicationSuccessfully()
	{
		// Arrange
		await SeedApplicationData();

		var existingApp = await _dbContext.AuthApplications
			.AsNoTracking()
			.FirstAsync(x => x.AppId == 2);

		var application = new EditApplicationDTO
		{
			AppId = existingApp!.AppId,
			AppName = existingApp.AppName + " Updated",
			Description = existingApp.Description + " Updated",
			IsActive = false
		};

		var command = new EditApplicationCommand(application);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result!.application.AppName.Should().Be(application.AppName);
		result!.application.Description.Should().Be(application.Description); 
		result!.application.IsActive.Should().BeFalse();
	}

	[Fact]
	public async Task EditApplication_ShouldThrow_WhenApplicationDoesNotExist()
	{
		// Arrange
		var application = new EditApplicationDTO
		{
			AppId = 1,
			AppName = "CNX",
			Description = "CNX Dashboard"
		};
		var command = new EditApplicationCommand(application);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Application with ID {application.AppId} was not found."); ;
	}

	[Fact]
	public async Task DeleteApplication_ShouldRemoveApplicationSuccessfully()
	{
		// Arrange
		await SeedApplicationData();
		var command = new DeleteApplicationCommand(1);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.IsDeleted.Should().BeTrue();
	}

	[Fact]
	public async Task DeleteApplication_ShouldThrow_WhenApplicationDoesNotExist()
	{
		// Arrange
		var command = new DeleteApplicationCommand(99); 

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Application with ID 99 was not found."); ;
	}

	private async Task SeedApplicationData()
	{
		var applications = new List<AuthApplication>
		{
			new AuthApplication
			{
				AppId = 1,
				AppName = "CNX",
				Description = "CNX Dashboard",
				IsActive = true
			},
			new AuthApplication
			{
				AppId = 2,
				AppName = "PhilSys",
				Description = "PhilSys IDV",
				IsActive = true
			},
			new AuthApplication
			{
				AppId = 3,
				AppName = "User Management",
				Description = "User Management",
				IsActive = true
			},
		};
		_dbContext.AuthApplications.AddRange(applications);
		await _dbContext.SaveChangesAsync();
	}
}
