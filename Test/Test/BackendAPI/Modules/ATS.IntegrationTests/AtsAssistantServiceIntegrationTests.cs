using ATS.Data.Entities;
using Auth.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class AtsAssistantServiceIntegrationTests : BaseIntegrationTest
{
	public AtsAssistantServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Search

	[Fact]
	public async Task SearchOrdersBySubjectAsync_ShouldReturnMatchingOrder_WhenNameMatches()
	{
		// Arrange
		var target = CreateInvitation("Russel", "Gutierrez", orderStatus: "In Progress");
		var other = CreateInvitation("Maria", "Santos", orderStatus: "Completed");
		await AddInvitationsAsync(target, other);

		// A platform super admin is scoped to every client
		SetSuperAdminUser();

		// Act
		var orders = await _atsAssistantService.SearchOrdersBySubjectAsync(
			"Russel Gutierrez",
			CancellationToken.None);

		// Assert
		orders.Should().ContainSingle();
		orders[0].EmailInvitationRequestId.Should().Be(target.EmailInvitationID);
		orders[0].SubjectName.Should().Be("Russel Gutierrez");
		orders[0].OrderStatus.Should().Be("In Progress");
	}

	[Fact]
	public async Task SearchOrdersBySubjectAsync_ShouldMatchPartialName()
	{
		// Arrange
		await AddInvitationsAsync(
			CreateInvitation("Russel", "Gutierrez", orderStatus: "In Progress"));

		SetSuperAdminUser();

		// Act
		var orders = await _atsAssistantService.SearchOrdersBySubjectAsync(
			"gutierrez",
			CancellationToken.None);

		// Assert
		orders.Should().ContainSingle();
		orders[0].SubjectName.Should().Be("Russel Gutierrez");
	}

	[Fact]
	public async Task SearchOrdersBySubjectAsync_ShouldReturnEmpty_WhenNobodyMatches()
	{
		// Arrange
		await AddInvitationsAsync(CreateInvitation("Maria", "Santos", orderStatus: "Completed"));

		SetSuperAdminUser();

		// Act
		var orders = await _atsAssistantService.SearchOrdersBySubjectAsync(
			"Russel Gutierrez",
			CancellationToken.None);

		// Assert
		orders.Should().BeEmpty();
	}

	#endregion

	#region Access scope

	[Fact]
	public async Task SearchOrdersBySubjectAsync_ShouldNotReturnOrdersOfAnotherRequestor()
	{
		// Arrange - the order belongs to a different requestor
		var order = CreateInvitation("Russel", "Gutierrez", orderStatus: "In Progress");
		order.ClientId = 7;
		order.RequestorId = Guid.CreateVersion7();
		await AddInvitationsAsync(order);

		// An ATS User is scoped to their own requests only
		SetCurrentUser(Guid.CreateVersion7(), roleId: 3, clientId: 7);

		// Act
		var orders = await _atsAssistantService.SearchOrdersBySubjectAsync(
			"Russel Gutierrez",
			CancellationToken.None);

		// Assert
		orders.Should().BeEmpty();
	}

	[Fact]
	public async Task SearchOrdersBySubjectAsync_ShouldReturnOwnOrders_WhenScopedToRequestor()
	{
		// Arrange
		var requestorId = Guid.CreateVersion7();
		var order = CreateInvitation("Russel", "Gutierrez", orderStatus: "In Progress");
		order.ClientId = 7;
		order.RequestorId = requestorId;
		await AddInvitationsAsync(order);

		SetCurrentUser(requestorId, roleId: 3, clientId: 7);

		// Act
		var orders = await _atsAssistantService.SearchOrdersBySubjectAsync(
			"Russel Gutierrez",
			CancellationToken.None);

		// Assert
		orders.Should().ContainSingle();
		orders[0].EmailInvitationRequestId.Should().Be(order.EmailInvitationID);
	}

	#endregion

	#region Confirm draft

	[Fact]
	public async Task ConfirmOrderDraftAsync_ShouldThrow_WhenDraftDoesNotExist()
	{
		// Act
		var act = () => _atsAssistantService.ConfirmOrderDraftAsync(
			Guid.CreateVersion7(),
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<Exception>()
			.Where(exception => exception.Message.Contains("expired"));
	}

	#endregion

	private void SetSuperAdminUser()
	{
		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity(
			[
				new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString()),
				new Claim(AuthClaimTypes.AtsRoleId, "1"),
				new Claim(AuthClaimTypes.PlatformRoleId, PlatformRoleIds.SuperAdmin.ToString())
			], "TestAuth"));
	}

	private void SetCurrentUser(Guid userId, int roleId, int? clientId = null)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString())
		};

		if (clientId.HasValue)
		{
			claims.Add(new Claim(AuthClaimTypes.AtsClientId, clientId.Value.ToString()));
		}

		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity(claims, "TestAuth"));
	}

	private static EmailInvitationRequest CreateInvitation(
		string firstName,
		string lastName,
		string orderStatus)
	{
		var id = Guid.CreateVersion7();
		var now = DateTime.UtcNow;

		return new EmailInvitationRequest
		{
			EmailInvitationID = id,
			FirstName = firstName,
			LastName = lastName,
			EmailAddress = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.com",
			MobileNumber = "09171234567",
			Requestor = "ATS Integration Tests",
			PackageId = DefaultPackageId,
			SelectPackage = "Basic Screening",
			RushNormal = "Normal",
			HashToken = $"hash-{id}",
			HashTokenCreatedAt = now,
			HashTokenExpiration = now.AddDays(1),
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Done",
			OrderStatus = orderStatus,
			OrderCreatedAt = now.AddDays(-2)
		};
	}

	private async Task AddInvitationsAsync(params EmailInvitationRequest[] invitations)
	{
		await _dbContext.EmailInvitationRequests.AddRangeAsync(invitations);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();
	}
}
