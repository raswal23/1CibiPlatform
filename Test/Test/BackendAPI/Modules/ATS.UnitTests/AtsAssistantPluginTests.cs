using ATS.Services.AccessScope;
using ATS.AI;
using ATS.Constants;
using ATS.DTO;
using ATS.Data.Repository;
using ATS.Services.OrderHistory;
using ATS.Shared.Implementations;
using Auth.Shared.Contracts;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Moq;
using ATS.Services.Settings.PackageManagement;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class AtsAssistantPluginTests
{
	private const int ClientId = 42;
	private static readonly Guid AuthenticatedUserId = Guid.CreateVersion7();

	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();
	private readonly Mock<IPackageManagementService> _packageManagementService = new();
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly Mock<IUserClientRepository> _userClientRepository = new();
	private readonly AtsOrderDraftStore _draftStore = new();

	public AtsAssistantPluginTests()
	{
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(false);
		_currentUser.SetupGet(user => user.UserId).Returns(AuthenticatedUserId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(AtsRoleIds.User);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(ClientId);

		SetupPackages("Standard Screening", "Executive Screening");
	}

	#region Search

	[Fact]
	public async Task SearchOrdersBySubjectAsync_ShouldReturnMatchingOrders_WhenNameMatches()
	{
		// Arrange
		var orderId = Guid.CreateVersion7();

		_repository
			.Setup(repository => repository.SearchReportsPageAsync(
				null,
				null,
				null,
				It.IsAny<int>(),
				"Russel Gutierrez",
				null,
				null,
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.Single() == ClientId),
				AuthenticatedUserId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new ReportRowDTO
				{
					EmailInvitationID = orderId,
					FirstName = "Russel",
					LastName = "Gutierrez",
					OrderStatus = "In Progress",
					SelectPackage = "Standard Screening",
					Requestor = "ATS User",
					HitStatus = "Clear"
				}
			]);

		var plugin = await CreatePluginAsync();

		// Act
		var orders = await plugin.SearchOrdersBySubjectAsync("Russel Gutierrez", CancellationToken.None);

		// Assert
		orders.Should().ContainSingle();
		orders[0].EmailInvitationRequestId.Should().Be(orderId);
		orders[0].SubjectName.Should().Be("Russel Gutierrez");
		orders[0].OrderStatus.Should().Be("In Progress");
		plugin.LastSearchResults.Should().ContainSingle();
	}

	[Fact]
	public async Task SearchOrdersBySubjectAsync_ShouldReturnEmpty_WhenNameIsBlank()
	{
		// Arrange
		var plugin = await CreatePluginAsync();

		// Act
		var orders = await plugin.SearchOrdersBySubjectAsync("   ", CancellationToken.None);

		// Assert
		orders.Should().BeEmpty();

		_repository.Verify(
			repository => repository.SearchReportsPageAsync(
				It.IsAny<int?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<Guid?>(),
				It.IsAny<int>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task SearchOrdersBySubjectAsync_ShouldReturnEmpty_WhenScopeIsDenied()
	{
		// Arrange - an unauthenticated caller resolves to the Denied scope
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(false);

		var plugin = await CreatePluginAsync();

		// Act
		var orders = await plugin.SearchOrdersBySubjectAsync("Russel Gutierrez", CancellationToken.None);

		// Assert
		orders.Should().BeEmpty();

		_repository.Verify(
			repository => repository.SearchReportsPageAsync(
				It.IsAny<int?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<Guid?>(),
				It.IsAny<int>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	#endregion

	#region Packages

	[Fact]
	public async Task GetAvailablePackagesAsync_ShouldReturnOnlyActivePackageNames()
	{
		// Arrange
		_packageManagementService
			.Setup(service => service.GetPackagesAsync(
				It.IsAny<KeysetPaginationRequest>(),
				It.IsAny<CancellationToken>(),
				ClientId))
			.ReturnsAsync(new KeysetPaginatedResult<PackageDetailsDTO>(
			[
				new PackageDetailsDTO { PackageId = 1, PackageName = "Standard Screening", IsActive = true },
				new PackageDetailsDTO { PackageId = 2, PackageName = "Retired Screening", IsActive = false }
			], null, 2));

		var plugin = await CreatePluginAsync();

		// Act
		var packages = await plugin.GetAvailablePackagesAsync(CancellationToken.None);

		// Assert
		packages.Should().ContainSingle().Which.Should().Be("Standard Screening");
	}

	#endregion

	#region Stage new order

	[Fact]
	public async Task StageNewOrderAsync_ShouldStageDraft_WhenDetailsAreValid()
	{
		// Arrange
		var plugin = await CreatePluginAsync();

		// Act
		var message = await plugin.StageNewOrderAsync(
			"Juan",
			"Dela Cruz",
			"juan.delacruz@example.com",
			"09171234567",
			"standard screening",
			"Rush",
			middleInitial: "M",
			CancellationToken.None);

		// Assert
		message.Should().Contain("NOT been created");
		plugin.StagedDraft.Should().NotBeNull();
		plugin.StagedDraft!.DraftId.Should().NotBe(Guid.Empty);
		plugin.StagedDraft.FirstName.Should().Be("Juan");
		plugin.StagedDraft.LastName.Should().Be("Dela Cruz");
		plugin.StagedDraft.MiddleInitial.Should().Be("M");
		plugin.StagedDraft.RushNormal.Should().Be("Rush");

		// The package name is normalized back to the stored casing
		plugin.StagedDraft.SelectPackage.Should().Be("Standard Screening");
	}

	[Theory]
	[InlineData("", "Dela Cruz", "juan@example.com", "09171234567", "first name")]
	[InlineData("Juan", "", "juan@example.com", "09171234567", "last name")]
	[InlineData("Juan", "Dela Cruz", "not-an-email", "09171234567", "email")]
	[InlineData("Juan", "Dela Cruz", "juan@example.com", "0917", "11 digits")]
	[InlineData("Juan", "Dela Cruz", "juan@example.com", "0917123456X", "11 digits")]
	public async Task StageNewOrderAsync_ShouldRejectInvalidDetails(
		string firstName,
		string lastName,
		string emailAddress,
		string mobileNumber,
		string expectedMessagePart)
	{
		// Arrange
		var plugin = await CreatePluginAsync();

		// Act
		var message = await plugin.StageNewOrderAsync(
			firstName,
			lastName,
			emailAddress,
			mobileNumber,
			"Standard Screening",
			"Normal",
			middleInitial: null,
			CancellationToken.None);

		// Assert
		message.Should().Contain(expectedMessagePart);
		plugin.StagedDraft.Should().BeNull();
	}

	[Fact]
	public async Task StageNewOrderAsync_ShouldRejectPackage_WhenNotAssignedToClient()
	{
		// Arrange
		var plugin = await CreatePluginAsync();

		// Act
		var message = await plugin.StageNewOrderAsync(
			"Juan",
			"Dela Cruz",
			"juan.delacruz@example.com",
			"09171234567",
			"Hallucinated Package",
			"Normal",
			middleInitial: null,
			CancellationToken.None);

		// Assert
		message.Should().Contain("not a package available");
		message.Should().Contain("Standard Screening");
		plugin.StagedDraft.Should().BeNull();
	}

	[Fact]
	public async Task StageNewOrderAsync_ShouldDefaultToNormal_WhenSpeedIsNotRush()
	{
		// Arrange
		var plugin = await CreatePluginAsync();

		// Act
		await plugin.StageNewOrderAsync(
			"Juan",
			"Dela Cruz",
			"juan.delacruz@example.com",
			"09171234567",
			"Standard Screening",
			"whenever",
			middleInitial: null,
			CancellationToken.None);

		// Assert
		plugin.StagedDraft!.RushNormal.Should().Be("Normal");
	}

	#endregion

	#region Draft store

	[Fact]
	public async Task DraftStore_ShouldConsumeDraftOnlyOnce()
	{
		// Arrange
		var plugin = await CreatePluginAsync();

		await plugin.StageNewOrderAsync(
			"Juan",
			"Dela Cruz",
			"juan.delacruz@example.com",
			"09171234567",
			"Standard Screening",
			"Normal",
			middleInitial: null,
			CancellationToken.None);

		var draftId = plugin.StagedDraft!.DraftId;

		// Act
		var first = _draftStore.Consume(draftId, AuthenticatedUserId);
		var second = _draftStore.Consume(draftId, AuthenticatedUserId);

		// Assert
		first.Should().NotBeNull();
		second.Should().BeNull();
	}

	[Fact]
	public async Task DraftStore_ShouldNotReleaseDraftToAnotherUser()
	{
		// Arrange
		var plugin = await CreatePluginAsync();

		await plugin.StageNewOrderAsync(
			"Juan",
			"Dela Cruz",
			"juan.delacruz@example.com",
			"09171234567",
			"Standard Screening",
			"Normal",
			middleInitial: null,
			CancellationToken.None);

		var draftId = plugin.StagedDraft!.DraftId;

		// Act
		var stolen = _draftStore.Consume(draftId, Guid.CreateVersion7());

		// Assert
		stolen.Should().BeNull();
		_draftStore.Consume(draftId, AuthenticatedUserId).Should().NotBeNull();
	}

	#endregion

	private void SetupPackages(params string[] packageNames)
	{
		var packages = packageNames
			.Select((name, index) => new PackageDetailsDTO
			{
				PackageId = index + 1,
				PackageName = name,
				IsActive = true
			})
			.ToArray();

		_packageManagementService
			.Setup(service => service.GetPackagesAsync(
				It.IsAny<KeysetPaginationRequest>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<int?>()))
			.ReturnsAsync(new KeysetPaginatedResult<PackageDetailsDTO>(packages, null, packages.Length));
	}

	private Task<AtsAssistantPlugin> CreatePluginAsync() =>
		Task.FromResult(new AtsAssistantPlugin(
			_repository.Object,
			_orderHistoryService.Object,
			_packageManagementService.Object,
			_draftStore,
			_currentUser.Object,
			// A real resolver over the mocked ICurrentUser, so these tests keep
			// asserting the scope the assistant actually gets.
			new AtsAccessScopeResolver(_currentUser.Object, _userClientRepository.Object)));
}
