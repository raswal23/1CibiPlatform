using ATS.Constants;
using ATS.Data.Repository;
using ATS.Data.Repository.Administration.UserClient;
using ATS.DTO;
using ATS.Services;
using ATS.Shared.Implementations;
using Auth.Shared.Contracts;
using FluentAssertions;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class DashboardServiceTests
{
	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IUserClientRepository> _userClientRepository = new();
	private readonly Mock<ICurrentUser> _currentUser = new();

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetDashboardAsync_ShouldUseAuthenticatedRequestorScope_ForUserAndUploader(int roleId)
	{
		var userId = Guid.CreateVersion7();
		var expected = new ATSDashboardDTO();
		SetupAuthenticatedUser(userId, roleId, clientId: 25);
		_repository.Setup(repository => repository.GetDashboardAsync(
			"Other Requester",
			AtsQueryScope.ForRequestor(userId),
			CancellationToken.None)).ReturnsAsync(expected);
		var service = CreateService();

		var result = await service.GetDashboardAsync("Other Requester", CancellationToken.None);

		result.Should().BeSameAs(expected);
		_userClientRepository.Verify(repository => repository.GetUserClientAssignmentsAsync(
			It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldPreserveClientScope_ForOtherClientRole()
	{
		var userId = Guid.CreateVersion7();
		var expected = new ATSDashboardDTO();
		SetupAuthenticatedUser(userId, roleId: 99, clientId: 25);
		_repository.Setup(repository => repository.GetDashboardAsync(
			null,
			AtsQueryScope.ForClientAndRequestor(25, userId),
			CancellationToken.None)).ReturnsAsync(expected);
		var service = CreateService();

		var result = await service.GetDashboardAsync(null, CancellationToken.None);

		result.Should().BeSameAs(expected);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldUseAllScope_ForPlatformSuperAdmin()
	{
		var userId = Guid.CreateVersion7();
		var expected = new ATSDashboardDTO();
		SetupAuthenticatedUser(userId, AtsRoleIds.User, clientId: null, isPlatformSuperAdmin: true);
		_repository.Setup(repository => repository.GetDashboardAsync(
			null, AtsQueryScope.All, CancellationToken.None)).ReturnsAsync(expected);
		var service = CreateService();

		var result = await service.GetDashboardAsync(null, CancellationToken.None);

		result.Should().BeSameAs(expected);
		_userClientRepository.Verify(repository => repository.GetUserClientAssignmentsAsync(
			It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Theory]
	[InlineData(AtsRoleIds.Admin)]
	[InlineData(AtsRoleIds.PlatformManager)]
	public async Task GetDashboardAsync_ShouldUseAssignedClientScope_ForManagerRoles(int roleId)
	{
		var userId = Guid.CreateVersion7();
		var expected = new ATSDashboardDTO();
		SetupAuthenticatedUser(userId, roleId, clientId: 999);
		_userClientRepository.Setup(repository => repository.GetUserClientAssignmentsAsync(
			It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.First() == userId),
			CancellationToken.None)).ReturnsAsync(
		[
			new UserClientDetailsDTO { UserId = userId, ClientId = 1 },
			new UserClientDetailsDTO { UserId = userId, ClientId = 3 },
			new UserClientDetailsDTO { UserId = userId, ClientId = 5 }
		]);
		int[] expectedClientIds = [1, 3, 5];
		_repository.Setup(repository => repository.GetDashboardAsync(
			null,
			It.Is<AtsQueryScope>(scope =>
				scope.Kind == AtsQueryScopeKind.Clients
				&& scope.ClientIds.SequenceEqual(expectedClientIds)),
			CancellationToken.None)).ReturnsAsync(expected);
		var service = CreateService();

		var result = await service.GetDashboardAsync(null, CancellationToken.None);

		result.Should().BeSameAs(expected);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldReturnEmptyDashboard_WhenManagerHasNoClientAssignment()
	{
		var userId = Guid.CreateVersion7();
		SetupAuthenticatedUser(userId, AtsRoleIds.Admin, clientId: null);
		_userClientRepository.Setup(repository => repository.GetUserClientAssignmentsAsync(
			It.IsAny<IReadOnlyCollection<Guid>>(), CancellationToken.None))
			.ReturnsAsync(Array.Empty<UserClientDetailsDTO>());
		var service = CreateService();

		var result = await service.GetDashboardAsync(null, CancellationToken.None);

		result.Should().BeEquivalentTo(new ATSDashboardDTO());
		VerifyDashboardRepositoryWasNotCalled();
	}

	[Theory]
	[InlineData(false, true, 1)]
	[InlineData(true, false, 1)]
	public async Task GetDashboardAsync_ShouldReturnEmptyDashboard_WhenIdentityIsUnavailable(
		bool isAuthenticated,
		bool hasUserId,
		int? clientId)
	{
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(isAuthenticated);
		_currentUser.SetupGet(user => user.UserId).Returns(hasUserId ? Guid.CreateVersion7() : null);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(AtsRoleIds.User);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(clientId);
		var service = CreateService();

		var result = await service.GetDashboardAsync(null, CancellationToken.None);

		result.Should().BeEquivalentTo(new ATSDashboardDTO());
		VerifyDashboardRepositoryWasNotCalled();
	}

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetDashboardAsync_ShouldUseRequestorScope_WhenUserOrUploaderHasNoClientClaim(
		int roleId)
	{
		var userId = Guid.CreateVersion7();
		var expected = new ATSDashboardDTO();
		SetupAuthenticatedUser(userId, roleId, clientId: null);
		_repository.Setup(repository => repository.GetDashboardAsync(
			null,
			AtsQueryScope.ForRequestor(userId),
			CancellationToken.None)).ReturnsAsync(expected);
		var service = CreateService();

		var result = await service.GetDashboardAsync(null, CancellationToken.None);

		result.Should().BeSameAs(expected);
	}

	private DashboardService CreateService() => new(
		_repository.Object,
		new AtsQueryScopeResolver(_currentUser.Object, _userClientRepository.Object));

	private void SetupAuthenticatedUser(
		Guid userId,
		int roleId,
		int? clientId,
		bool isPlatformSuperAdmin = false)
	{
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(roleId);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(clientId);
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(isPlatformSuperAdmin);
	}

	private void VerifyDashboardRepositoryWasNotCalled() =>
		_repository.Verify(repository => repository.GetDashboardAsync(
			It.IsAny<string?>(),
			It.IsAny<AtsQueryScope>(),
			It.IsAny<CancellationToken>()), Times.Never);
}
