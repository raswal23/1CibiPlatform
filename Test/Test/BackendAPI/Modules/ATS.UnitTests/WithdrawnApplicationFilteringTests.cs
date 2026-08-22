using ATS.Constants;
using ATS.Data.Repository;
using ATS.Data.Repository.Administration.UserClient;
using ATS.Data.UnitOfWork;
using ATS.DTO;
using ATS.Services.AccessScope;
using ATS.Services.EndorsementSubmission;
using ATS.Services.OrderHistory;
using Auth.Shared.Contracts;
using BuildingBlocks.Pagination;
using BuildingBlocks.SharedServices.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class WithdrawnApplicationFilteringTests
{
	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IUserClientRepository> _userClientRepository = new();
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly EndorsementSubmissionService _service;

	public WithdrawnApplicationFilteringTests()
	{
		_service = new EndorsementSubmissionService(
			Mock.Of<ILogger<EndorsementSubmissionService>>(),
			_repository.Object,
			new ConfigurationBuilder().Build(),
			Mock.Of<IHashService>(),
			Mock.Of<IEmailService>(),
			Mock.Of<HybridCache>(),
			Mock.Of<ISecureToken>(),
			new HttpContextAccessor(),
			_currentUser.Object,
			Mock.Of<IObjectStorageService>(),
			Mock.Of<IOrderHistoryService>(),
			_userClientRepository.Object,
			// A real resolver over the same mocks. These tests are specifically about
			// which clients/requestor a role resolves to, so stubbing the resolver would
			// remove the thing under test - and Mock.Of<> returns null, which the
			// service reads as "no access".
			new AtsAccessScopeResolver(_currentUser.Object, _userClientRepository.Object),
			Mock.Of<IUnitOfWork>());
	}

	[Theory]
	[InlineData(AtsRoleIds.PlatformManager)]
	[InlineData(AtsRoleIds.Admin)]
	public async Task GetWithdrawnEmailInvitationRequestsAsync_ShouldUseAssignedClientsForManagerRoles(
		int roleId)
	{
		var userId = SetAuthenticatedUser(roleId, clientId: 99);
		var request = new KeysetPaginationRequest(null, 10, "withdrawn");
		_userClientRepository.Setup(repository => repository.GetUserClientAssignmentsAsync(
			It.Is<IReadOnlyCollection<Guid>>(userIds => userIds.SequenceEqual(new[] { userId })),
			CancellationToken.None)).ReturnsAsync(
			[
				new UserClientDetailsDTO { UserId = userId, ClientId = 1 },
				new UserClientDetailsDTO { UserId = userId, ClientId = 3 }
			]);
		_repository.Setup(repository => repository.GetWithdrawnPageAsync(
			"withdrawn",
			null,
			11,
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 1, 3 })),
			null,
			CancellationToken.None)).ReturnsAsync([]);
		_repository.Setup(repository => repository.CountWithdrawnAsync(
			"withdrawn",
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 1, 3 })),
			null,
			CancellationToken.None)).ReturnsAsync(0);

		var result = await _service.GetWithdrawnEmailInvitationRequestsAsync(
			request,
			CancellationToken.None);

		result.Items.Should().BeEmpty();
		result.TotalCount.Should().Be(0);
		_repository.VerifyAll();
	}

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetWithdrawnEmailInvitationRequestsAsync_ShouldUseOwnClientAndRequestorForRestrictedRoles(
		int roleId)
	{
		var userId = SetAuthenticatedUser(roleId, clientId: 7);
		var request = new KeysetPaginationRequest(null, 10);
		_repository.Setup(repository => repository.GetWithdrawnPageAsync(
			null,
			null,
			11,
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 7 })),
			userId,
			CancellationToken.None)).ReturnsAsync([]);
		_repository.Setup(repository => repository.CountWithdrawnAsync(
			null,
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 7 })),
			userId,
			CancellationToken.None)).ReturnsAsync(0);

		var result = await _service.GetWithdrawnEmailInvitationRequestsAsync(
			request,
			CancellationToken.None);

		result.Items.Should().BeEmpty();
		_repository.VerifyAll();
	}

	[Fact]
	public async Task GetWithdrawnEmailInvitationRequestsAsync_ShouldBypassAllDataFilters_ForPlatformSuperAdmin()
	{
		SetAuthenticatedUser(AtsRoleIds.User, clientId: 7);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns((int?)null);
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(true);
		var request = new KeysetPaginationRequest(null, 10);
		_repository.Setup(repository => repository.GetWithdrawnPageAsync(
			null,
			null,
			11,
			null,
			null,
			CancellationToken.None)).ReturnsAsync([]);
		_repository.Setup(repository => repository.CountWithdrawnAsync(
			null,
			null,
			null,
			CancellationToken.None)).ReturnsAsync(0);

		var result = await _service.GetWithdrawnEmailInvitationRequestsAsync(
			request,
			CancellationToken.None);

		result.Items.Should().BeEmpty();
		_repository.VerifyAll();
		_userClientRepository.Verify(repository => repository.GetUserClientAssignmentsAsync(
			It.IsAny<IReadOnlyCollection<Guid>>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	private Guid SetAuthenticatedUser(int roleId, int clientId)
	{
		var userId = Guid.CreateVersion7();
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(roleId);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(clientId);
		return userId;
	}
}
