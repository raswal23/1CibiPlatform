using Auth.Data.Entities;
using Auth.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class ApplicationServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;

	public ApplicationServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task GetApplicationsAsync_ShouldReturnPaginatedResult()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(
			Cursor: null,
			PageSize: 10,
			SearchTerm: null);

		var applicationData = new List<ApplicationsDTO>
		{
			new ApplicationsDTO(1, "PhilSys", "PhilSys IDV", true),
			new ApplicationsDTO(2, "CNX", "CNX Dashboard", true)
		};

		_fixture.MockAuthRepository
			.Setup(x => x.GetApplicationsPageAsync(null, null, 11, CancellationToken.None))
			.ReturnsAsync(applicationData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountApplicationsAsync(null, CancellationToken.None))
			.ReturnsAsync(2);

		// Act
		var result = await _fixture.ApplicationService.GetApplicationsAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(2);
		result.NextCursor.Should().BeNull();
		result.Items.Should().BeEquivalentTo(applicationData);
	}

	[Fact]
	public async Task GetApplicationsAsync_ShouldPassSearchTerm_WhenProvided()
	{
		// Arrange
		var paginationRequest = new KeysetPaginationRequest(
			Cursor: null,
			PageSize: 10,
			SearchTerm: "PhilSys");

		var applicationData = new List<ApplicationsDTO>
		{
			new ApplicationsDTO(1, "PhilSys", "PhilSys IDV", true),
		};

		_fixture.MockAuthRepository
			.Setup(x => x.GetApplicationsPageAsync("PhilSys", null, 11, CancellationToken.None))
			.ReturnsAsync(applicationData.ToList());
		_fixture.MockAuthRepository
			.Setup(x => x.CountApplicationsAsync("PhilSys", CancellationToken.None))
			.ReturnsAsync(1);

		// Act
		var result = await _fixture.ApplicationService.GetApplicationsAsync(paginationRequest, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.TotalCount.Should().Be(1);
		result.NextCursor.Should().BeNull();
		result.Items.Should().BeEquivalentTo(applicationData);
	}

	[Fact]
	public async Task DeleteApplicationAsync_ShouldThrow_WhenNotFound()
	{
		// Arrange
		var appId = 99;
		_fixture.MockAuthRepository
			.Setup(x => x.GetApplicationAsync(appId))
			.ReturnsAsync((AuthApplication)null);

		// Act
		Func<Task> act = async () => await _fixture.ApplicationService.DeleteApplicationAsync(appId);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"Application with ID {appId} was not found.");
	}

	[Fact]
	public async Task DeleteApplicationAsync_ShouldReturnTrue_WhenDeleted()
	{
		// Arrange
		var appId = 1;
		var existingApp = new AuthApplication { AppId = appId, AppName = "PhilSys", Description = "PhilSys IDV", IsActive = true };

		_fixture.MockAuthRepository
			.Setup(x => x.GetApplicationAsync(appId))
			.ReturnsAsync(existingApp);

		_fixture.MockAuthRepository
			.Setup(x => x.DeleteApplicationAsync(existingApp))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.ApplicationService.DeleteApplicationAsync(appId);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task AddApplicationAsync_ShouldReturnTrue_WhenAdded()
	{
		// Arrange
		var application = new AddApplicationDTO { AppName = "PhilSys", Description = "PhilSys IDV", IsActive = true};

		_fixture.MockAuthRepository
			.Setup(x => x.AddApplicationAsync(application))
			.ReturnsAsync(true);

		// Act
		var result = await _fixture.ApplicationService.AddApplicationAsync(application);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task EditApplicationAsync_ShouldThrow_WhenApplicationNotFound()
	{
		// Arrange
		var editDto = new EditApplicationDTO { AppId = 99, AppName = "UpdatedApp", Description = "Updated", IsActive = false };

		_fixture.MockAuthRepository
			.Setup(x => x.GetApplicationAsync(editDto.AppId))
			.ReturnsAsync((AuthApplication)null);

		// Act
		Func<Task> act = async () => await _fixture.ApplicationService.EditApplicationAsync(editDto);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage($"Application with ID {editDto.AppId} was not found.");
	}

	[Fact]
	public async Task EditApplicationAsync_ShouldReturnUpdatedDto_WhenSuccessful()
	{
		// Arrange
		var editDto = new EditApplicationDTO { AppId = 1, AppName = "UpdatedApp", Description = "Updated", IsActive = false };
		var existingApp = new AuthApplication { AppId = 1, AppName = "OldApp", Description = "Old" , IsActive = true };
		var updatedApp = new AuthApplication { AppId = 1, AppName = "UpdatedApp", Description = "Updated" , IsActive = false };

		_fixture.MockAuthRepository
			.Setup(x => x.GetApplicationAsync(editDto.AppId))
			.ReturnsAsync(existingApp);

		_fixture.MockAuthRepository
			.Setup(x => x.EditApplicationAsync(existingApp))
			.ReturnsAsync(updatedApp);

		// Act
		var result = await _fixture.ApplicationService.EditApplicationAsync(editDto);

		// Assert
		result.Should().NotBeNull();
		result.AppId.Should().Be(updatedApp.AppId);
		result.AppName.Should().Be(updatedApp.AppName);
		result.Description.Should().Be(updatedApp.Description);
	}
}
