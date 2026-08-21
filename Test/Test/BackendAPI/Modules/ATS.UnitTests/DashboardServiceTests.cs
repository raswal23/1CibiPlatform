using ATS.Constants;
using ATS.Data.Entities;
using ATS.Data.Repository;
using ATS.DTO;
using ATS.Services.Dashboard;
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
	[InlineData(AtsRoleIds.PlatformManager)]
	[InlineData(AtsRoleIds.Admin)]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetDashboardAsync_ShouldPassAuthenticatedIdentityToRepository(int roleId)
	{
		var userId = Guid.CreateVersion7();
		const int clientId = 5;
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(clientId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(roleId);
		_userClientRepository.Setup(repository => repository.GetUserClientAssignmentsAsync(
			It.Is<IReadOnlyCollection<Guid>>(userIds => userIds.SequenceEqual(new[] { userId })),
			CancellationToken.None)).ReturnsAsync(
			[
				new UserClientDetailsDTO { UserId = userId, ClientId = 1 },
				new UserClientDetailsDTO { UserId = userId, ClientId = 3 },
				new UserClientDetailsDTO { UserId = userId, ClientId = 5 }
			]);
		var expectedClientIds = roleId is AtsRoleIds.PlatformManager or AtsRoleIds.Admin
			? new[] { 1, 3, 5 }
			: new[] { clientId };
		var expectedRequestorId = roleId is AtsRoleIds.User or AtsRoleIds.Uploader
			? userId
			: (Guid?)null;
		_repository.Setup(repository => repository.GetDashboardDataAsync(
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(expectedClientIds)),
			expectedRequestorId,
			CancellationToken.None)).ReturnsAsync(Array.Empty<EmailInvitationRequest>());
		var service = new DashboardService(
			_repository.Object,
			_userClientRepository.Object,
			_currentUser.Object);

		var result = await service.GetDashboardAsync(
			"Selected Requester",
			CancellationToken.None);

		result.Requesters.Should().BeEmpty();
		result.YtdHireSeries.Should().BeEmpty();
		result.RecentOrders.Should().BeEmpty();
		result.CandidateResponseRate.Categories
			.Select(category => category.Name)
			.Should().Equal("Completed", "Incomplete", "Not Started");
		result.CandidateResponseRate.Categories.Should().OnlyContain(category =>
			category.Count == 0 && category.Percentage == 0);
		result.TurnaroundTimeTrend
			.Select(series => series.Name)
			.Should().Equal("Complete", "Closed", "Clear", "Not Clear");
		result.TurnaroundTimeTrend.Should().OnlyContain(series =>
			series.Points.Count == 7 && series.Points.All(point => point.Count == 0));
		result.CompletionRate.Categories
			.Select(category => category.Name)
			.Should().Equal("Complete", "Closed", "Initial", "Supplementary");
		result.CompletionRate.Categories.Should().OnlyContain(category =>
			category.Count == 0 && category.Percentage == 0);
		_repository.Verify(repository => repository.GetDashboardDataAsync(
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(expectedClientIds)),
			expectedRequestorId,
			CancellationToken.None), Times.Once);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldBypassAllDataFilters_ForPlatformSuperAdmin()
	{
		var userId = Guid.CreateVersion7();
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns((int?)null);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(5);
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(true);
		_repository.Setup(repository => repository.GetDashboardDataAsync(
			null,
			null,
			CancellationToken.None)).ReturnsAsync(Array.Empty<EmailInvitationRequest>());
		var service = new DashboardService(
			_repository.Object,
			_userClientRepository.Object,
			_currentUser.Object);

		await service.GetDashboardAsync(null, CancellationToken.None);

		_repository.Verify(repository => repository.GetDashboardDataAsync(
			null,
			null,
			CancellationToken.None), Times.Once);
		_userClientRepository.Verify(repository => repository.GetUserClientAssignmentsAsync(
			It.IsAny<IReadOnlyCollection<Guid>>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Theory]
	[InlineData(false, true, true, true)]
	[InlineData(true, false, true, true)]
	[InlineData(true, true, false, true)]
	[InlineData(true, true, true, false)]
	public async Task GetDashboardAsync_ShouldReturnEmptyDashboard_WhenIdentityIsUnavailable(
		bool isAuthenticated,
		bool hasUserId,
		bool hasClientId,
		bool hasRoleId)
	{
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(isAuthenticated);
		_currentUser.SetupGet(user => user.UserId)
			.Returns(hasUserId ? Guid.CreateVersion7() : null);
		_currentUser.SetupGet(user => user.AtsClientId)
			.Returns(hasClientId ? 5 : null);
		_currentUser.SetupGet(user => user.AtsRoleId)
			.Returns(hasRoleId ? AtsRoleIds.User : null);
		var service = new DashboardService(
			_repository.Object,
			_userClientRepository.Object,
			_currentUser.Object);

		var result = await service.GetDashboardAsync(null, CancellationToken.None);

		result.Should().BeEquivalentTo(new ATSDashboardDTO());
		_repository.Verify(repository => repository.GetDashboardDataAsync(
			It.IsAny<IReadOnlyCollection<int>>(),
			It.IsAny<Guid?>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}
}
