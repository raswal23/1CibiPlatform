using ATS.Constants;
using ATS.Data.Entities;
using ATS.DTO;
using Auth.Constants;
using FluentAssertions;
using System.Security.Claims;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class DashboardServiceIntegrationTests : BaseIntegrationTest
{
	public DashboardServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldIncludeAssignedClientsAndExcludeUnassignedClient_ForPlatformManager()
	{
		var userId = Guid.CreateVersion7();
		await AddInvitationsAsync(
			CreateInvitation("Assigned One", 1, userId, "Platform Manager"),
			CreateInvitation("Same Assigned Client", 1, Guid.CreateVersion7(), "Other Requester"),
			CreateInvitation("Unassigned", 2, Guid.CreateVersion7(), "Excluded Requester"));
		await AddAssignmentAsync(userId, clientId: 1);
		SetAuthenticatedUser(userId, AtsRoleIds.PlatformManager, claimedClientId: 99);

		var dashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		AssertDashboardScope(
			dashboard,
			["Assigned One Subject", "Same Assigned Client Subject"],
			["Platform Manager", "Other Requester"]);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldIncludeAssignedClientAndExcludeUnassignedClient_ForAdmin()
	{
		var userId = Guid.CreateVersion7();
		await AddInvitationsAsync(
			CreateInvitation("Own Request", 3, userId, "Admin"),
			CreateInvitation("Same Client", 3, Guid.CreateVersion7(), "Other Requester"),
			CreateInvitation("Other Client", 4, Guid.CreateVersion7(), "Excluded Requester"));
		await AddAssignmentAsync(userId, clientId: 3);
		SetAuthenticatedUser(userId, AtsRoleIds.Admin, claimedClientId: 99);

		var dashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		AssertDashboardScope(
			dashboard,
			["Own Request Subject", "Same Client Subject"],
			["Admin", "Other Requester"]);
	}

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetDashboardAsync_ShouldRequireOwnRequestorAndClient_ForRestrictedRoles(
		int roleId)
	{
		var userId = Guid.CreateVersion7();
		await AddClientsAsync((7, true), (8, true));
		await AddInvitationsAsync(
			CreateInvitation("Matching", 7, userId, "Current User"),
			CreateInvitation("Wrong Requester", 7, Guid.CreateVersion7(), "Other User"),
			CreateInvitation("Wrong Client", 8, userId, "Current User"));
		SetAuthenticatedUser(userId, roleId, claimedClientId: 7);

		var dashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);
		var dashboardForOtherRequester = await _dashboardService.GetDashboardAsync(
			"Other User",
			CancellationToken.None);

		AssertDashboardScope(
			dashboard,
			["Matching Subject"],
			["Current User"]);
		AssertEmptyDashboard(dashboardForOtherRequester);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldIncludeAllClientsAndRequesters_ForPlatformSuperAdmin()
	{
		var userId = Guid.CreateVersion7();
		await AddInvitationsAsync(
			CreateInvitation("Client One", 1, Guid.CreateVersion7(), "Requester One"),
			CreateInvitation("Client Two", 2, Guid.CreateVersion7(), "Requester Two"));
		SetAuthenticatedUser(
			userId,
			AtsRoleIds.User,
			claimedClientId: 99,
			isPlatformSuperAdmin: true);

		var dashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		AssertDashboardScope(
			dashboard,
			["Client One Subject", "Client Two Subject"],
			["Requester One", "Requester Two"]);
	}

	private void SetAuthenticatedUser(
		Guid userId,
		int roleId,
		int claimedClientId,
		bool isPlatformSuperAdmin = false)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString()),
			new(AuthClaimTypes.AtsClientId, claimedClientId.ToString())
		};
		if (isPlatformSuperAdmin)
		{
			claims.Add(new Claim(
				AuthClaimTypes.PlatformRoleId,
				PlatformRoleIds.SuperAdmin.ToString()));
		}

		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity(claims, "TestAuth"));
	}

	private async Task AddClientsAsync(params (int ClientId, bool IsActive)[] clients)
	{
		var now = DateTime.UtcNow;

		// BaseIntegrationTest already seeds a package at DefaultPackageId after every
		// truncate, so this links the clients to that one rather than inserting a
		// second row with the same key.
		await _dbContext.ClientDetails.AddRangeAsync(clients.Select(client => new ClientDetails
		{
			ClientId = client.ClientId,
			ClientName = $"Client {client.ClientId}",
			ClientDescription = $"Client {client.ClientId}",
			IsActive = client.IsActive,
			PackageId = DefaultPackageId,
			CreatedAt = now,
			UpdatedAt = now
		}));
		await _dbContext.SaveChangesAsync();
	}

	private async Task AddAssignmentAsync(Guid userId, int clientId)
	{
		var now = DateTime.UtcNow;
		await _dbContext.UserClientDetails.AddAsync(new UserClientDetails
		{
			UserId = userId,
			ClientId = clientId,
			CreatedAt = now,
			UpdatedAt = now
		});
		await _dbContext.SaveChangesAsync();
	}

	private async Task AddInvitationsAsync(params EmailInvitationRequest[] invitations)
	{
		await _dbContext.EmailInvitationRequests.AddRangeAsync(invitations);
		await _dbContext.SaveChangesAsync();
	}

	private static void AssertDashboardScope(
		ATSDashboardDTO dashboard,
		IReadOnlyCollection<string> expectedSubjectNames,
		IReadOnlyCollection<string> expectedRequesters)
	{
		dashboard.Requesters.Should().BeEquivalentTo(expectedRequesters);
		dashboard.RecentOrders.Select(order => order.SubjectName)
			.Should().BeEquivalentTo(expectedSubjectNames);
		dashboard.YtdHireSeries.Sum(series => series.Points.Sum(point => point.Count))
			.Should().Be(expectedSubjectNames.Count);
		dashboard.CandidateResponseRate.Categories.Sum(category => category.Count)
			.Should().Be(expectedSubjectNames.Count);
		dashboard.CompletionRate.Categories.Single(category => category.Name == "Complete").Count
			.Should().Be(expectedSubjectNames.Count);
		dashboard.CompletionRate.Categories.Where(category => category.Name != "Complete")
			.Should().OnlyContain(category => category.Count == 0);
		dashboard.TurnaroundTimeTrend.Single(series => series.Name == "Complete").Points
			.Sum(point => point.Count).Should().Be(expectedSubjectNames.Count);
	}

	private static void AssertEmptyDashboard(ATSDashboardDTO dashboard)
	{
		dashboard.Requesters.Should().ContainSingle("Current User");
		dashboard.RecentOrders.Should().BeEmpty();
		dashboard.YtdHireSeries.Should().BeEmpty();
		dashboard.CandidateResponseRate.Categories.Sum(category => category.Count).Should().Be(0);
		dashboard.CompletionRate.Categories.Sum(category => category.Count).Should().Be(0);
		dashboard.TurnaroundTimeTrend
			.SelectMany(series => series.Points).Sum(point => point.Count).Should().Be(0);
	}

	private static EmailInvitationRequest CreateInvitation(
		string firstName,
		int clientId,
		Guid requestorId,
		string requestor)
	{
		var id = Guid.CreateVersion7();
		var now = DateTime.UtcNow;
		return new EmailInvitationRequest
		{
			EmailInvitationID = id,
			FirstName = firstName,
			LastName = "Subject",
			EmailAddress = $"{id:N}@example.com",
			MobileNumber = "+639171234567",
			Requestor = requestor,
			RequestorId = requestorId,
			ClientId = clientId,
			PackageId = DefaultPackageId,
			SelectPackage = "Dashboard Package",
			RushNormal = "Normal",
			HashToken = $"hash-{id}",
			HashTokenCreatedAt = now,
			HashTokenExpiration = now.AddDays(1),
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Done",
			OrderStatus = "Completed",
			OrderCreatedAt = now.AddDays(-1),
			OrderCompletedAt = now,
			ReportDetails =
			[
				new ReportDetails
				{
					ReportFileId = Guid.CreateVersion7(),
					EmailInvitationRequestId = id,
					HitStatus = "Clear",
					ReportStatus = "Complete Final Report",
					ReportFileName = $"{id:N}.pdf",
					ReportFileKey = $"reports/{id:N}.pdf",
					ReportUploadedAt = now
				}
			]
		};
	}
}
