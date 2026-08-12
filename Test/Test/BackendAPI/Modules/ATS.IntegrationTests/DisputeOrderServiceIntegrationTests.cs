using System.Security.Claims;
using ATS.Data.Entities;
using ATS.Data.Repository.Administration.UserClient;
using ATS.DTO;
using ATS.Services;
using ATS.Constants;
using ATS.Data.Repository.Administration.Clients;
using ATS.Shared.Implementations;
using Auth.Constants;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using BuildingBlocks.SharedServices.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class DisputeOrderServiceIntegrationTests : BaseIntegrationTest
{
	private const string CompanyName = "Integration Test Company";
	private static readonly Guid AuthenticatedUserId = Guid.CreateVersion7();

	public DisputeOrderServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldReturnEligibleOrdersInDisputePriorityOrder()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var disputed = CreateOrder(
			"Disputed",
			"Candidate",
			"disputed@example.com",
			now.AddDays(-10),
			now.AddDays(-9),
			"Report");
		var newest = CreateOrder(
			"Newest",
			"Candidate",
			"newest@example.com",
			now.AddDays(-2),
			now.AddDays(-1));
		var oldest = CreateOrder(
			"Oldest",
			"Candidate",
			"oldest@example.com",
			now.AddDays(-20),
			now.AddDays(-19));
		var outsideDisputeWindow = CreateOrder(
			"Expired",
			"Candidate",
			"expired@example.com",
			now.AddDays(-40),
			now.AddDays(-31));
		var incomplete = CreateOrder(
			"Incomplete",
			"Candidate",
			"incomplete@example.com",
			now.AddDays(-2),
			now.AddDays(-1),
			orderStatus: "In Progress");

		await AddOrdersAsync(disputed, newest, oldest, outsideDisputeWindow, incomplete);
		var service = CreateService(CreateSuccessfulEmailService());

		// Act
		var result = await service.GetDisputeOrdersAsync(
			new PaginationRequest(PageIndex: 1, PageSize: 2),
			CancellationToken.None);

		// Assert
		result.PageIndex.Should().Be(1);
		result.PageSize.Should().Be(2);
		result.Count.Should().Be(3);

		var orders = result.Data.ToArray();
		orders.Select(order => order.EmailInvitationID)
			.Should().Equal(disputed.EmailInvitationID, newest.EmailInvitationID);
		orders[0].Should().BeEquivalentTo(new
		{
			disputed.EmailInvitationID,
			disputed.FirstName,
			disputed.LastName,
			disputed.DisputeCategory
		});
		orders[0].OrderCreatedAt.Should().BeCloseTo(
			disputed.OrderCreatedAt!.Value,
			TimeSpan.FromMilliseconds(1));
		orders[0].OrderCompletedAt.Should().BeCloseTo(
			disputed.OrderCompletedAt!.Value,
			TimeSpan.FromMilliseconds(1));
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldSearchEligibleOrdersCaseInsensitively()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var firstNameMatch = CreateOrder(
			"Needle",
			"First",
			"first@example.com",
			now.AddDays(-5),
			now.AddDays(-4));
		var lastNameMatch = CreateOrder(
			"Second",
			"NEEDLETON",
			"second@example.com",
			now.AddDays(-4),
			now.AddDays(-3));
		var emailMatch = CreateOrder(
			"Third",
			"Candidate",
			"contains.needle@example.com",
			now.AddDays(-3),
			now.AddDays(-2));
		var nonMatch = CreateOrder(
			"Different",
			"Candidate",
			"different@example.com",
			now.AddDays(-2),
			now.AddDays(-1));
		var expiredMatch = CreateOrder(
			"Needle",
			"Expired",
			"expired.needle@example.com",
			now.AddDays(-40),
			now.AddDays(-31));

		await AddOrdersAsync(firstNameMatch, lastNameMatch, emailMatch, nonMatch, expiredMatch);
		var service = CreateService(CreateSuccessfulEmailService());

		// Act
		var result = await service.GetDisputeOrdersAsync(
			new PaginationRequest(PageIndex: 1, PageSize: 10, SearchTerm: "needle"),
			CancellationToken.None);

		// Assert
		result.Count.Should().Be(3);
		result.Data.Select(order => order.EmailInvitationID).Should().BeEquivalentTo([
			firstNameMatch.EmailInvitationID,
			lastNameMatch.EmailInvitationID,
			emailMatch.EmailInvitationID
		]);
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldEnforceRoleBasedScopes()
	{
		var userId = Guid.CreateVersion7();
		var uploaderId = Guid.CreateVersion7();
		var adminId = Guid.CreateVersion7();
		var managerId = Guid.CreateVersion7();
		var superAdminId = Guid.CreateVersion7();
		var userOrder = CreateOrder("User", "Candidate", "user@example.com", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
		userOrder.ClientId = 1;
		userOrder.RequestorId = userId;
		var uploaderOrder = CreateOrder("Uploader", "Candidate", "uploader@example.com", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
		uploaderOrder.ClientId = 2;
		uploaderOrder.RequestorId = uploaderId;
		var adminOrder = CreateOrder("Admin", "Candidate", "admin@example.com", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
		adminOrder.ClientId = 3;
		adminOrder.RequestorId = Guid.CreateVersion7();
		var managerOrder = CreateOrder("Manager", "Candidate", "manager@example.com", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
		managerOrder.ClientId = 4;
		managerOrder.RequestorId = Guid.CreateVersion7();
		var unauthorized = CreateOrder("Unauthorized", "Candidate", "unauthorized@example.com", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
		unauthorized.ClientId = 5;
		unauthorized.RequestorId = Guid.CreateVersion7();
		await AddOrdersAsync(userOrder, uploaderOrder, adminOrder, managerOrder, unauthorized);
		await _dbContext.UserClientDetails.AddRangeAsync(
			new UserClientDetails { UserId = adminId, ClientId = 3 },
			new UserClientDetails { UserId = managerId, ClientId = 4 });
		await _dbContext.SaveChangesAsync();
		var service = CreateService(CreateSuccessfulEmailService());
		var request = new PaginationRequest(PageIndex: 1, PageSize: 20);

		SetDisputeScope(userId, AtsRoleIds.User, clientId: 999);
		var userResult = await service.GetDisputeOrdersAsync(request, CancellationToken.None);
		SetDisputeScope(uploaderId, AtsRoleIds.Uploader, clientId: 999);
		var uploaderResult = await service.GetDisputeOrdersAsync(request, CancellationToken.None);
		SetDisputeScope(adminId, AtsRoleIds.Admin, clientId: 999);
		var adminResult = await service.GetDisputeOrdersAsync(request, CancellationToken.None);
		SetDisputeScope(managerId, AtsRoleIds.PlatformManager, clientId: 999);
		var managerResult = await service.GetDisputeOrdersAsync(request, CancellationToken.None);
		SetDisputeScope(superAdminId, AtsRoleIds.User, null, isPlatformSuperAdmin: true);
		var allResult = await service.GetDisputeOrdersAsync(request, CancellationToken.None);

		userResult.Data.Should().ContainSingle(order => order.EmailInvitationID == userOrder.EmailInvitationID);
		uploaderResult.Data.Should().ContainSingle(order => order.EmailInvitationID == uploaderOrder.EmailInvitationID);
		adminResult.Data.Should().ContainSingle(order => order.EmailInvitationID == adminOrder.EmailInvitationID);
		managerResult.Data.Should().ContainSingle(order => order.EmailInvitationID == managerOrder.EmailInvitationID);
		allResult.Count.Should().Be(5);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldSendEmailPersistDisputeAndInvalidateCachedList()
	{
		// Arrange
		var order = CreateOrder(
			"Ada",
			"Lovelace",
			"ada@example.com",
			DateTime.UtcNow.AddDays(-2),
			DateTime.UtcNow.AddDays(-1));
		order.ClientId = 7;
		await AddOrdersAsync(order);

		const string requestor = "requestor@example.com";
		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity([
				new Claim(ClaimTypes.Email, requestor)
			], "TestAuth"));

		var emailService = CreateSuccessfulEmailService();
		var service = CreateService(emailService);
		var pagination = new PaginationRequest(PageIndex: 1, PageSize: 10);
		var cachedBeforeUpdate = await service.GetDisputeOrdersAsync(
			pagination,
			CancellationToken.None);
		cachedBeforeUpdate.Data.Should().ContainSingle().Which.DisputeCategory.Should().BeNull();

		var request = new DisputeOrderRequestDTO
		{
			EmailInvitationId = order.EmailInvitationID,
			DisputeReason = "Report"
		};
		var startedAt = DateTime.UtcNow;

		// Act
		var result = await service.MarkAsDisputedAsync(request, CancellationToken.None);

		// Assert
		result.Should().BeTrue();
		_dbContext.ChangeTracker.Clear();

		var persisted = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == order.EmailInvitationID);
		persisted.DisputeCategory.Should().Be("Report");
		persisted.DisputedAt.Should().NotBeNull();
		persisted.DisputedAt!.Value.Should().BeOnOrAfter(startedAt);
		persisted.DisputedAt.Value.Should().BeOnOrBefore(DateTime.UtcNow);

		var refreshed = await service.GetDisputeOrdersAsync(pagination, CancellationToken.None);
		refreshed.Data.Should().ContainSingle().Which.DisputeCategory.Should().Be("Report");

		var recipient = _configuration["ATS:DisputeOrderEmailRecipient"] ?? string.Empty;
		emailService.Verify(serviceMock => serviceMock.SendEmailForDispute(
			recipient,
			CompanyName,
			"Report",
			It.Is<DateTime?>(value => value.HasValue
				&& order.OrderCreatedAt.HasValue
				&& Math.Abs((value.Value - order.OrderCreatedAt.Value).TotalMilliseconds) < 1),
			requestor,
			"Ada Lovelace"), Times.Once);
		emailService.Verify(serviceMock => serviceMock.SendATSEmailAsync(
			recipient,
			"CIBI | Dispute Order Notification",
			"dispute-email-body"), Times.Once);
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldThrowAndPreserveOrder_WhenEmailCannotBeSent()
	{
		// Arrange
		var order = CreateOrder(
			"Email",
			"Failure",
			"email.failure@example.com",
			DateTime.UtcNow.AddDays(-2),
			DateTime.UtcNow.AddDays(-1));
		order.ClientId = 7;
		await AddOrdersAsync(order);

		var emailService = new Mock<IEmailService>();
		emailService
			.Setup(service => service.SendEmailForDispute(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.Returns("dispute-email-body");
		emailService
			.Setup(service => service.SendATSEmailAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync(false);

		var service = CreateService(emailService);
		var request = CreateDisputeRequest(order);

		// Act
		Func<Task> act = () => service.MarkAsDisputedAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("Failed to send dispute order notification email.");

		_dbContext.ChangeTracker.Clear();
		var persisted = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == order.EmailInvitationID);
		persisted.DisputeCategory.Should().BeNull();
		persisted.DisputedAt.Should().BeNull();
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldPropagateCancellationAndPreserveOrder()
	{
		// Arrange
		var order = CreateOrder(
			"Repository",
			"Failure",
			"repository.failure@example.com",
			DateTime.UtcNow.AddDays(-2),
			DateTime.UtcNow.AddDays(-1));
		await AddOrdersAsync(order);

		var emailService = CreateSuccessfulEmailService();
		var service = CreateService(emailService);
		var request = CreateDisputeRequest(order);
		using var cancellationSource = new CancellationTokenSource();
		cancellationSource.Cancel();

		// Act
		Func<Task> act = () => service.MarkAsDisputedAsync(request, cancellationSource.Token);

		// Assert
		await act.Should()
			.ThrowAsync<OperationCanceledException>();

		_dbContext.ChangeTracker.Clear();
		var persisted = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == order.EmailInvitationID);
		persisted.DisputeCategory.Should().BeNull();
		persisted.DisputedAt.Should().BeNull();
		emailService.Verify(serviceMock => serviceMock.SendATSEmailAsync(
			It.IsAny<string>(),
			It.IsAny<string>(),
			It.IsAny<string>()), Times.Never);
	}

	#endregion

	private DisputeOrderService CreateService(Mock<IEmailService> emailService)
	{
		var userClientRepository = new Mock<IUserClientRepository>();
		userClientRepository
			.Setup(repository => repository.GetUserClientAssignmentsAsync(
				It.Is<IReadOnlyCollection<Guid>>(userIds =>
					userIds.Count == 1 && userIds.Contains(AuthenticatedUserId)),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([
				new UserClientDetailsDTO
				{
					UserId = AuthenticatedUserId,
					ClientId = 7,
					ClientName = CompanyName
				}
			]);

		var clientRepository = new Mock<IClientRepository>();
		clientRepository.Setup(repository => repository.GetClientAsync(
			It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([
			new ClientDetails { ClientName = CompanyName }
		]);

		return new DisputeOrderService(
			NullLogger<DisputeOrderService>.Instance,
			emailService.Object,
			_configuration,
			_atsRepository,
			clientRepository.Object,
			_httpContextAccessor,
			new AtsQueryScopeResolver(
				CreateAllClientsCurrentUser().Object,
				userClientRepository.Object));
	}

	private void SetDisputeScope(
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

	private static Mock<ICurrentUser> CreateAllClientsCurrentUser()
	{
		var currentUser = new Mock<ICurrentUser>();
		currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		currentUser.SetupGet(user => user.UserId).Returns(AuthenticatedUserId);
		currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(true);
		return currentUser;
	}

	private static Mock<IEmailService> CreateSuccessfulEmailService()
	{
		var emailService = new Mock<IEmailService>();
		emailService
			.Setup(service => service.SendEmailForDispute(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.Returns("dispute-email-body");
		emailService
			.Setup(service => service.SendATSEmailAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync(true);

		return emailService;
	}

	private async Task AddOrdersAsync(params EmailInvitationRequest[] orders)
	{
		await _dbContext.EmailInvitationRequests.AddRangeAsync(orders);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();
	}

	private static DisputeOrderRequestDTO CreateDisputeRequest(EmailInvitationRequest order) => new()
	{
		EmailInvitationId = order.EmailInvitationID,
		DisputeReason = "Billing"
	};

	private static EmailInvitationRequest CreateOrder(
		string firstName,
		string lastName,
		string email,
		DateTime orderCreatedAt,
		DateTime orderCompletedAt,
		string? disputeCategory = null,
		string orderStatus = "Completed")
	{
		var id = Guid.CreateVersion7();
		var now = DateTime.UtcNow;

		return new EmailInvitationRequest
		{
			EmailInvitationID = id,
			FirstName = firstName,
			LastName = lastName,
			MiddleInitial = firstName[..1],
			EmailAddress = email,
			MobileNumber = "+639171234567",
			Requestor = "ATS Integration Tests",
			SelectPackage = "Basic Screening",
			RushNormal = "Normal",
			HashToken = $"hash-{id}",
			HashTokenCreatedAt = now,
			HashTokenExpiration = now.AddDays(1),
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Done",
			OrderStatus = orderStatus,
			OrderCreatedAt = orderCreatedAt,
			OrderCompletedAt = orderCompletedAt,
			DisputeCategory = disputeCategory
		};
	}
}
