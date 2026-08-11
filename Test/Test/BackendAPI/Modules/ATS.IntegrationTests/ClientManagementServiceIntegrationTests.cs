using ATS.DTO;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class ClientManagementServiceIntegrationTests : BaseIntegrationTest
{
	public ClientManagementServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task AddClientAsync_ShouldPersistOneLogicalClientWithAllSelectedPackages()
	{
		// Arrange
		var basicPackageId = await AddPackageAsync("Basic Screening");
		var premiumPackageId = await AddPackageAsync("Premium Screening");
		var request = CreateAddRequest(
			"  Acme Corporation  ",
			"  Primary enterprise client  ",
			true,
			basicPackageId,
			premiumPackageId);

		// Act
		var result = await _clientManagementService.AddClientAsync(request, CancellationToken.None);

		// Assert
		result.Should().BeTrue();

		var persisted = await _dbContext.ClientDetails
			.AsNoTracking()
			.OrderBy(client => client.PackageId)
			.ToListAsync();

		persisted.Should().HaveCount(2);
		persisted.Select(client => client.ClientId).Distinct().Should().ContainSingle();
		persisted.Select(client => client.PackageId).Should().Equal(basicPackageId, premiumPackageId);
		persisted.Should().OnlyContain(client =>
			client.ClientName == "Acme Corporation" &&
			client.ClientDescription == "Primary enterprise client" &&
			client.IsActive);
		persisted.Should().OnlyContain(client =>
			client.CreatedAt <= DateTime.UtcNow &&
			client.CreatedAt >= DateTime.UtcNow.AddSeconds(-5));
		persisted.Should().OnlyContain(client => client.UpdatedAt == client.CreatedAt);
	}

	[Fact]
	public async Task GetClientsAsync_ShouldPaginateLogicalClientsAndReturnEveryPackageAssignment()
	{
		// Arrange
		var basicPackageId = await AddPackageAsync("Basic Package");
		var premiumPackageId = await AddPackageAsync("Premium Package");

		await AddClientAsync("Zulu Client", "Third client", basicPackageId);
		await AddClientAsync("Alpha Client", "First client", basicPackageId, premiumPackageId);
		await AddClientAsync("Middle Client", "Second client", premiumPackageId);

		var request = new PaginationRequest(PageIndex: 1, PageSize: 2);

		// Act
		var result = await _clientManagementService.GetClientsAsync(request, CancellationToken.None);

		// Assert
		result.PageIndex.Should().Be(1);
		result.PageSize.Should().Be(2);
		result.Count.Should().Be(3);
		result.Data.Should().HaveCount(3);
		result.Data.Select(client => client.ClientName).Distinct()
			.Should().Equal("Alpha Client", "Middle Client");
		result.Data.Where(client => client.ClientName == "Alpha Client")
			.Select(client => client.PackageId)
			.Should().Equal(basicPackageId, premiumPackageId);
	}

	[Fact]
	public async Task GetClientsAsync_ShouldSearchLogicalClientsCaseInsensitivelyAndReturnAllAssignments()
	{
		// Arrange
		var basicPackageId = await AddPackageAsync("Basic Search Package");
		var premiumPackageId = await AddPackageAsync("Premium Search Package");

		await AddClientAsync("Basic Client", "Standard screening customer", basicPackageId);
		await AddClientAsync("Executive Client", "Requires PREMIUM screening", basicPackageId, premiumPackageId);
		await AddClientAsync("Premium Partner", "Complex checks", premiumPackageId);

		var request = new PaginationRequest(PageIndex: 1, PageSize: 10, SearchTerm: "premium");

		// Act
		var result = await _clientManagementService.GetClientsAsync(request, CancellationToken.None);

		// Assert
		result.Count.Should().Be(2);
		result.Data.Should().HaveCount(3);
		result.Data.Select(client => client.ClientName).Distinct()
			.Should().Equal("Executive Client", "Premium Partner");
		result.Data.Where(client => client.ClientName == "Executive Client")
			.Select(client => client.PackageId)
			.Should().Equal(basicPackageId, premiumPackageId);
	}

	[Fact]
	public async Task EditClientAsync_ShouldSynchronizePackagesAndUpdateSharedClientDetails()
	{
		// Arrange
		var removedPackageId = await AddPackageAsync("Removed Package");
		var retainedPackageId = await AddPackageAsync("Retained Package");
		var addedPackageId = await AddPackageAsync("Added Package");

		await AddClientAsync("Original Client", "Original description", removedPackageId, retainedPackageId);

		var existing = await _dbContext.ClientDetails
			.AsNoTracking()
			.OrderBy(client => client.PackageId)
			.ToListAsync();
		var clientId = existing[0].ClientId;
		var createdAt = existing[0].CreatedAt;

		_dbContext.ChangeTracker.Clear();

		var request = CreateEditRequest(
			clientId,
			"Updated Client",
			"Updated description",
			false,
			retainedPackageId,
			addedPackageId);

		// Act
		var result = await _clientManagementService.EditClientAsync(request, CancellationToken.None);

		// Assert
		result.Should().HaveCount(2);
		result.Select(client => client.PackageId).Should().Equal(retainedPackageId, addedPackageId);
		result.Should().OnlyContain(client =>
			client.ClientId == clientId &&
			client.ClientName == "Updated Client" &&
			client.ClientDescription == "Updated description" &&
			!client.IsActive);

		var persisted = await _dbContext.ClientDetails
			.AsNoTracking()
			.Where(client => client.ClientId == clientId)
			.OrderBy(client => client.PackageId)
			.ToListAsync();

		persisted.Select(client => client.PackageId).Should().Equal(retainedPackageId, addedPackageId);
		persisted.Should().OnlyContain(client => client.CreatedAt == createdAt);
		persisted.Should().OnlyContain(client => client.UpdatedAt >= createdAt);
		persisted.Should().OnlyContain(client =>
			client.ClientName == "Updated Client" &&
			client.ClientDescription == "Updated description" &&
			!client.IsActive);
		persisted.Should().NotContain(client => client.PackageId == removedPackageId);
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task AddClientAsync_ShouldThrowBadRequestException_WhenClientNameAlreadyExists()
	{
		// Arrange
		var packageId = await AddPackageAsync("Duplicate Client Package");
		await AddClientAsync("Acme Client", "Original description", packageId);

		var duplicate = CreateAddRequest(
			"  acme client  ",
			"Duplicate description",
			false,
			packageId);

		// Act
		Func<Task> act = () => _clientManagementService.AddClientAsync(duplicate, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("Client 'acme client' already exists.");

		var logicalClientCount = await _dbContext.ClientDetails
			.AsNoTracking()
			.Select(client => client.ClientId)
			.Distinct()
			.CountAsync();

		logicalClientCount.Should().Be(1);
	}

	[Fact]
	public async Task AddClientAsync_ShouldThrowBadRequestException_WhenSelectedPackageIsInactive()
	{
		// Arrange
		var activePackageId = await AddPackageAsync("Active Client Package");
		var inactivePackageId = await AddPackageAsync("Inactive Client Package", isActive: false);
		var request = CreateAddRequest(
			"Invalid Package Client",
			"Contains an inactive package",
			true,
			activePackageId,
			inactivePackageId);

		// Act
		Func<Task> act = () => _clientManagementService.AddClientAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("One or more selected packages do not exist or are inactive.");

		var clientWasPersisted = await _dbContext.ClientDetails.AsNoTracking().AnyAsync();
		clientWasPersisted.Should().BeFalse();
	}

	[Fact]
	public async Task EditClientAsync_ShouldThrowNotFoundException_WhenClientDoesNotExist()
	{
		// Arrange
		var packageId = await AddPackageAsync("Missing Client Package");
		var request = CreateEditRequest(
			int.MaxValue,
			"Missing Client",
			"Missing description",
			true,
			packageId);

		// Act
		Func<Task> act = () => _clientManagementService.EditClientAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage($"Client with ID {int.MaxValue} was not found.");
	}

	[Fact]
	public async Task EditClientAsync_ShouldRejectInactiveNewPackageAndPreserveExistingAssignments()
	{
		// Arrange
		var existingPackageId = await AddPackageAsync("Existing Client Package");
		var inactivePackageId = await AddPackageAsync("Inactive New Package", isActive: false);
		await AddClientAsync("Unchanged Client", "Original description", existingPackageId);

		var existing = await _dbContext.ClientDetails
			.AsNoTracking()
			.SingleAsync();

		_dbContext.ChangeTracker.Clear();

		var request = CreateEditRequest(
			existing.ClientId,
			"Changed Client",
			"Changed description",
			false,
			existingPackageId,
			inactivePackageId);

		// Act
		Func<Task> act = () => _clientManagementService.EditClientAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("One or more newly selected packages do not exist or are inactive.");

		var persisted = await _dbContext.ClientDetails
			.AsNoTracking()
			.SingleAsync();

		persisted.ClientName.Should().Be("Unchanged Client");
		persisted.ClientDescription.Should().Be("Original description");
		persisted.IsActive.Should().BeTrue();
		persisted.PackageId.Should().Be(existingPackageId);
	}

	#endregion

	private async Task<int> AddPackageAsync(string name, bool isActive = true)
	{
		await _packageManagementService.AddPackageAsync(new AddPackageDTO
		{
			PackageName = name,
			PackageDescription = $"Description for {name}",
			IsActive = isActive,
			FollowUpEmail = 1
		}, CancellationToken.None);

		return await _dbContext.PackageDetails
			.AsNoTracking()
			.Where(package => package.PackageName == name)
			.Select(package => package.PackageId)
			.SingleAsync();
	}

	private Task<bool> AddClientAsync(string name, string description, params int[] packageIds) =>
		_clientManagementService.AddClientAsync(
			CreateAddRequest(name, description, true, packageIds),
			CancellationToken.None);

	private static AddClientDTO[] CreateAddRequest(
		string name,
		string description,
		bool isActive,
		params int[] packageIds) =>
		packageIds.Select(packageId => new AddClientDTO
		{
			ClientName = name,
			ClientDescription = description,
			IsActive = isActive,
			PackageId = packageId
		}).ToArray();

	private static EditClientDTO[] CreateEditRequest(
		int clientId,
		string name,
		string description,
		bool isActive,
		params int[] packageIds) =>
		packageIds.Select(packageId => new EditClientDTO
		{
			ClientId = clientId,
			ClientName = name,
			ClientDescription = description,
			IsActive = isActive,
			PackageId = packageId
		}).ToArray();
}
