using ATS.Data.Entities;
using ATS.Data.Repository;
using ATS.DTO;
using ATS.Services.AccessScope;
using ATS.Services.Dashboard;
using FluentAssertions;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

// The role-to-scope ladder moved into AtsAccessScopeResolver, so these tests no longer
// stub ICurrentUser / IUserClientRepository. They assert what DashboardService does with
// a scope, not how the scope was derived.
public class DashboardServiceTests
{
	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IAtsAccessScopeResolver> _accessScopeResolver = new();

	private DashboardService CreateService() =>
		new(_repository.Object, _accessScopeResolver.Object);

	private void SetAccessScope(IReadOnlyCollection<int>? authorizedClientIds, Guid? requiredOwnerId)
	{
		_accessScopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AtsAccessScope(authorizedClientIds, requiredOwnerId));
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldPassResolvedScopeToRepository()
	{
		var userId = Guid.CreateVersion7();
		var expectedClientIds = new[] { 1, 3, 5 };
		SetAccessScope(expectedClientIds, userId);
		_repository.Setup(repository => repository.GetDashboardDataAsync(
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(expectedClientIds)),
			userId,
			It.IsAny<DateTime>(),
			CancellationToken.None)).ReturnsAsync(Array.Empty<EmailInvitationRequest>());

		var result = await CreateService().GetDashboardAsync(
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
			userId,
			It.IsAny<DateTime>(),
			CancellationToken.None), Times.Once);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldBypassAllDataFilters_ForPlatformSuperAdmin()
	{
		// (null, null) is the super admin scope: no client predicate and no owner
		// predicate. null is not the same as an empty collection.
		SetAccessScope(null, null);
		_repository.Setup(repository => repository.GetDashboardDataAsync(
			null,
			null,
			It.IsAny<DateTime>(),
			CancellationToken.None)).ReturnsAsync(Array.Empty<EmailInvitationRequest>());

		await CreateService().GetDashboardAsync(null, CancellationToken.None);

		_repository.Verify(repository => repository.GetDashboardDataAsync(
			null,
			null,
			It.IsAny<DateTime>(),
			CancellationToken.None), Times.Once);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldReturnEmptyDashboard_WhenCallerHasNoScope()
	{
		_accessScopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync((AtsAccessScope?)null);

		var result = await CreateService().GetDashboardAsync(null, CancellationToken.None);

		result.Should().BeEquivalentTo(new ATSDashboardDTO());
		_repository.Verify(repository => repository.GetDashboardDataAsync(
			It.IsAny<IReadOnlyCollection<int>>(),
			It.IsAny<Guid?>(),
			It.IsAny<DateTime>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldBoundTheQueryWindow()
	{
		// The window has to reach back at least to the start of the calendar year, or
		// the YTD series would be missing its earliest months.
		SetAccessScope(null, null);
		DateTime? capturedWindowStart = null;
		_repository.Setup(repository => repository.GetDashboardDataAsync(
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<DateTime>(),
				It.IsAny<CancellationToken>()))
			.Callback<IReadOnlyCollection<int>?, Guid?, DateTime, CancellationToken>(
				(_, _, windowStart, _) => capturedWindowStart = windowStart)
			.ReturnsAsync(Array.Empty<EmailInvitationRequest>());

		await CreateService().GetDashboardAsync(null, CancellationToken.None);

		var now = DateTime.UtcNow;
		var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		capturedWindowStart.Should().NotBeNull();
		capturedWindowStart!.Value.Should().BeOnOrBefore(yearStart);
		capturedWindowStart.Value.Should().BeAfter(now.AddMonths(-13));
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldCapRecentOrders()
	{
		SetAccessScope(null, null);

		// 40 in scope, well over the 25 the panel carries.
		var invitations = Enumerable.Range(0, 40)
			.Select(index => new EmailInvitationRequest
			{
				EmailInvitationID = Guid.CreateVersion7(),
				FirstName = $"Subject{index}",
				LastName = "Test",
				OrderStatus = "Completed",
				OrderCreatedAt = DateTime.UtcNow.AddDays(-index)
			})
			.ToArray();
		_repository.Setup(repository => repository.GetDashboardDataAsync(
			It.IsAny<IReadOnlyCollection<int>?>(),
			It.IsAny<Guid?>(),
			It.IsAny<DateTime>(),
			It.IsAny<CancellationToken>())).ReturnsAsync(invitations);

		var result = await CreateService().GetDashboardAsync(null, CancellationToken.None);

		result.RecentOrders.Should().HaveCount(25);

		// Newest first, so the most recent order leads.
		result.RecentOrders[0].SubjectName.Should().Be("Subject0 Test");
	}
}
