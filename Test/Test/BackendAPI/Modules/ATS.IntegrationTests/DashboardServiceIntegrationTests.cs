using ATS.Data.Entities;
using ATS.Data.Repository;
using ATS.Constants;
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
	public async Task GetDashboardAsync_ShouldRequireMatchingClientAndRequestorIdsForEverySection()
	{
		var currentUserId = Guid.CreateVersion7();
		var otherUserId = Guid.CreateVersion7();

		var matching = CreateInvitation("Matching", clientId: 1, requestorId: currentUserId, requestor: "Current User");
		matching.ReportDetails = [CreateReport(matching.EmailInvitationID, "Complete Final Report")];
		var wrongClient = CreateInvitation("Wrong Client", clientId: 2, requestorId: currentUserId, requestor: "Current User");
		wrongClient.ReportDetails = [CreateReport(wrongClient.EmailInvitationID, "Initial Report")];
		var wrongRequestor = CreateInvitation("Wrong Requestor", clientId: 1, requestorId: otherUserId, requestor: "Other User");
		wrongRequestor.ReportDetails = [CreateReport(wrongRequestor.EmailInvitationID, "Supplementary Report")];
		await _dbContext.EmailInvitationRequests.AddRangeAsync(matching, wrongClient, wrongRequestor);
		await _dbContext.SaveChangesAsync();

		var dashboard = await _atsRepository.GetDashboardAsync(
			null,
			AtsQueryScope.ForClientAndRequestor(1, currentUserId),
			CancellationToken.None);

		dashboard.Requesters.Should().Equal("Current User");
		dashboard.YtdHireSeries.Should().ContainSingle(series =>
			series.Name == "Current User" && series.Points.Sum(point => point.Count) == 1);
		dashboard.CandidateResponseRate.Categories.Sum(category => category.Count).Should().Be(1);
		dashboard.CompletionRate.Categories.Single(category => category.Name == "Complete").Count.Should().Be(1);
		dashboard.CompletionRate.Categories.Where(category => category.Name != "Complete")
			.Should().OnlyContain(category => category.Count == 0);
		dashboard.RecentOrders.Should().ContainSingle(order => order.SubjectName == "Matching Subject");
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldUseAuthenticatedIdsInsteadOfRequesterQueryParameter()
	{
		var currentUserId = Guid.CreateVersion7();
		var otherUserId = Guid.CreateVersion7();
		await _dbContext.EmailInvitationRequests.AddRangeAsync(
			CreateInvitation("Matching", clientId: 25, requestorId: currentUserId, requestor: "Current User"),
			CreateInvitation("Other", clientId: 25, requestorId: otherUserId, requestor: "Other User"));
		await _dbContext.SaveChangesAsync();

		var dashboard = await _atsRepository.GetDashboardAsync(
			"Other User",
			AtsQueryScope.ForClientAndRequestor(25, currentUserId),
			CancellationToken.None);

		dashboard.Requesters.Should().Equal("Current User");
		dashboard.YtdHireSeries.Should().BeEmpty();
		dashboard.RecentOrders.Should().BeEmpty();
	}

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetDashboardAsync_ShouldUseAuthenticatedUserIdAsRequestor_ForUserAndUploader(
		int roleId)
	{
		var currentUserId = Guid.CreateVersion7();
		var otherUserId = Guid.CreateVersion7();
		await _dbContext.EmailInvitationRequests.AddRangeAsync(
			CreateInvitation("Matching First Client", 1, currentUserId, "Current User"),
			CreateInvitation("Matching Second Client", 2, currentUserId, "Current User"),
			CreateInvitation("Other Requestor", 1, otherUserId, "Other User"));
		await _dbContext.SaveChangesAsync();
		SetAuthenticatedUser(currentUserId, roleId, clientId: 999);

		var dashboard = await _dashboardService.GetDashboardAsync(
			null,
			CancellationToken.None);
		var dashboardForOtherRequester = await _dashboardService.GetDashboardAsync(
			"Other User",
			CancellationToken.None);

		dashboard.RecentOrders.Select(order => order.SubjectName).Should().BeEquivalentTo(
			["Matching First Client Subject", "Matching Second Client Subject"]);
		dashboard.Requesters.Should().Equal("Current User");
		dashboardForOtherRequester.RecentOrders.Should().BeEmpty();
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldReturnAllClientsWithoutRequestorRestriction_ForSuperAdminScope()
	{
		var first = CreateInvitation("First", 1, Guid.CreateVersion7(), "First Requester");
		var second = CreateInvitation("Second", 2, Guid.CreateVersion7(), "Second Requester");
		var third = CreateInvitation("Third", 3, Guid.CreateVersion7(), "Third Requester");
		await _dbContext.EmailInvitationRequests.AddRangeAsync(first, second, third);
		await _dbContext.SaveChangesAsync();

		var dashboard = await _atsRepository.GetDashboardAsync(
			null,
			AtsQueryScope.All,
			CancellationToken.None);

		dashboard.RecentOrders.Select(order => order.SubjectName)
			.Should().BeEquivalentTo(["First Subject", "Second Subject", "Third Subject"]);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldReturnOnlyAssignedClientsWithoutRequestorRestriction_ForManagerScope()
	{
		var clientOne = CreateInvitation("Client One", 1, Guid.CreateVersion7(), "First Requester");
		var clientThree = CreateInvitation("Client Three", 3, Guid.CreateVersion7(), "Third Requester");
		var unauthorized = CreateInvitation("Unauthorized", 2, Guid.CreateVersion7(), "Second Requester");
		await _dbContext.EmailInvitationRequests.AddRangeAsync(clientOne, clientThree, unauthorized);
		await _dbContext.SaveChangesAsync();

		var dashboard = await _atsRepository.GetDashboardAsync(
			null,
			AtsQueryScope.ForClients([1, 3]),
			CancellationToken.None);

		dashboard.RecentOrders.Select(order => order.SubjectName)
			.Should().BeEquivalentTo(["Client One Subject", "Client Three Subject"]);
		dashboard.Requesters.Should().BeEquivalentTo(["First Requester", "Third Requester"]);
	}

	[Fact]
	public async Task GetDashboardAsync_ShouldResolveAndEnforceEveryAuthenticatedRoleScope()
	{
		var normalUserId = Guid.CreateVersion7();
		var uploaderId = Guid.CreateVersion7();
		var adminOneId = Guid.CreateVersion7();
		var adminTwoId = Guid.CreateVersion7();
		var platformManagerId = Guid.CreateVersion7();
		var superAdminId = Guid.CreateVersion7();
		var clientOneForNormalUser = CreateInvitation("Normal Matching", 1, normalUserId, "Normal User");
		var clientOneForOtherUser = CreateInvitation("Admin One Client", 1, Guid.CreateVersion7(), "Client One User");
		var clientTwo = CreateInvitation("Admin Two Client", 2, Guid.CreateVersion7(), "Client Two User");
		var clientThree = CreateInvitation("Manager Client", 3, Guid.CreateVersion7(), "Client Three User");
		var normalUserWrongClient = CreateInvitation("Normal Wrong Client", 2, normalUserId, "Normal User");
		var normalUserWrongRequestor = CreateInvitation("Normal Wrong Requestor", 1, Guid.CreateVersion7(), "Other User");
		var uploaderMatching = CreateInvitation("Uploader Matching", 5, uploaderId, "Uploader");
		var uploaderWrongClient = CreateInvitation("Uploader Wrong Client", 6, uploaderId, "Uploader");
		var uploaderWrongRequestor = CreateInvitation("Uploader Wrong Requestor", 5, Guid.CreateVersion7(), "Other Uploader");
		foreach (var invitation in new[]
			{
				clientOneForNormalUser,
				clientOneForOtherUser,
				clientTwo,
				clientThree,
				normalUserWrongClient,
				normalUserWrongRequestor,
				uploaderMatching,
				uploaderWrongClient,
				uploaderWrongRequestor
			})
		{
			invitation.ReportDetails = [CreateReport(invitation.EmailInvitationID, "Complete Final Report")];
		}

		await _dbContext.EmailInvitationRequests.AddRangeAsync(
			clientOneForNormalUser,
			clientOneForOtherUser,
			clientTwo,
			clientThree,
			normalUserWrongClient,
			normalUserWrongRequestor,
			uploaderMatching,
			uploaderWrongClient,
			uploaderWrongRequestor);
		await _dbContext.UserClientDetails.AddRangeAsync(
			CreateAssignment(adminOneId, 1),
			CreateAssignment(adminTwoId, 2),
			CreateAssignment(platformManagerId, 3));
		await _dbContext.SaveChangesAsync();

		SetAuthenticatedUser(normalUserId, AtsRoleIds.User, clientId: 1);
		var normalDashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		SetAuthenticatedUser(uploaderId, AtsRoleIds.Uploader, clientId: 5);
		var uploaderDashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		SetAuthenticatedUser(adminOneId, AtsRoleIds.Admin, clientId: 999);
		var adminOneDashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		SetAuthenticatedUser(adminTwoId, AtsRoleIds.Admin, clientId: 999);
		var adminTwoDashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		SetAuthenticatedUser(platformManagerId, AtsRoleIds.PlatformManager, clientId: 999);
		var managerDashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		SetAuthenticatedUser(superAdminId, AtsRoleIds.User, clientId: null, isPlatformSuperAdmin: true);
		var superAdminDashboard = await _dashboardService.GetDashboardAsync(null, CancellationToken.None);

		AssertDashboardScope(normalDashboard,
			["Normal Matching Subject", "Normal Wrong Client Subject"]);
		AssertDashboardScope(uploaderDashboard,
			["Uploader Matching Subject", "Uploader Wrong Client Subject"]);
		AssertDashboardScope(adminOneDashboard,
			["Normal Matching Subject", "Admin One Client Subject", "Normal Wrong Requestor Subject"]);
		AssertDashboardScope(adminTwoDashboard,
			["Admin Two Client Subject", "Normal Wrong Client Subject"]);
		AssertDashboardScope(managerDashboard, ["Manager Client Subject"]);
		AssertDashboardScope(superAdminDashboard,
		[
			"Normal Matching Subject",
			"Admin One Client Subject",
			"Admin Two Client Subject",
			"Manager Client Subject",
			"Normal Wrong Client Subject",
			"Normal Wrong Requestor Subject",
			"Uploader Matching Subject",
			"Uploader Wrong Client Subject",
			"Uploader Wrong Requestor Subject"
		]);
	}

	private void SetAuthenticatedUser(
		Guid userId,
		int roleId,
		int? clientId,
		bool isPlatformSuperAdmin = false)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString())
		};
		if (clientId.HasValue)
			claims.Add(new Claim(AuthClaimTypes.AtsClientId, clientId.Value.ToString()));
		if (isPlatformSuperAdmin)
			claims.Add(new Claim(AuthClaimTypes.PlatformRoleId, PlatformRoleIds.SuperAdmin.ToString()));

		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity(claims, "TestAuth"));
	}

	private static void AssertDashboardScope(
		ATSDashboardDTO dashboard,
		IReadOnlyCollection<string> expectedSubjectNames)
	{
		dashboard.RecentOrders.Select(order => order.SubjectName)
			.Should().BeEquivalentTo(expectedSubjectNames);
		dashboard.YtdHireSeries.Sum(series => series.Points.Sum(point => point.Count))
			.Should().Be(expectedSubjectNames.Count);
		dashboard.CandidateResponseRate.Categories.Sum(category => category.Count)
			.Should().Be(expectedSubjectNames.Count);
		dashboard.CompletionRate.Categories.Single(category => category.Name == "Complete").Count
			.Should().Be(expectedSubjectNames.Count);
		dashboard.TurnaroundTimeTrend.Single(series => series.Name == "Complete").Points.Sum(point => point.Count)
			.Should().Be(expectedSubjectNames.Count);
	}

	private static UserClientDetails CreateAssignment(Guid userId, int clientId) => new()
	{
		UserId = userId,
		ClientId = clientId,
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

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
			SelectPackage = "Basic Screening",
			RushNormal = "Normal",
			HashToken = $"hash-{id}",
			HashTokenCreatedAt = now,
			HashTokenExpiration = now.AddDays(1),
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Done",
			OrderStatus = "Completed",
			OrderCreatedAt = now.AddDays(-1),
			OrderCompletedAt = now
		};
	}

	private static ReportDetails CreateReport(Guid invitationId, string reportStatus) => new()
	{
		ReportFileId = Guid.CreateVersion7(),
		EmailInvitationRequestId = invitationId,
		HitStatus = "Clear",
		ReportStatus = reportStatus,
		ReportFileName = $"{invitationId:N}.pdf",
		ReportFileKey = $"reports/{invitationId:N}.pdf",
		ReportUploadedAt = DateTime.UtcNow
	};
}
