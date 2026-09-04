using System.Security.Claims;
using ATS.Services.AccessScope;
using ATS.Constants;
using ATS.Data.Entities;
using ATS.Data.Repository;
using ATS.Data.UnitOfWork;
using ATS.DTO;
using ATS.Services.DisputeOrder;
using ATS.Services.OrderHistory;
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
	public async Task GetDisputeOrdersAsync_ShouldReturnEligibleOrdersByOrderCreatedAtDescending()
	{
		// Arrange
		SetAuthenticatedUser(AuthenticatedUserId, AtsRoleIds.User, clientId: 7);
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
		foreach (var order in new[] { disputed, newest, oldest, outsideDisputeWindow, incomplete })
		{
			order.ClientId = 7;
			order.RequestorId = AuthenticatedUserId;
		}

		await AddOrdersAsync(disputed, newest, oldest, outsideDisputeWindow, incomplete);
		var service = CreateService(CreateSuccessfulEmailService());

		// Act
		var result = await service.GetDisputeOrdersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 2),
			CancellationToken.None);

		// Assert
		result.TotalCount.Should().Be(3);
		result.NextCursor.Should().NotBeNull();

		var orders = result.Items.ToArray();
		orders.Select(order => order.EmailInvitationID)
			.Should().Equal(newest.EmailInvitationID, disputed.EmailInvitationID);
		orders[0].Should().BeEquivalentTo(new
		{
			newest.EmailInvitationID,
			newest.FirstName,
			newest.LastName,
			newest.DisputeCategory
		});
		orders[0].OrderCreatedAt.Should().BeCloseTo(
			newest.OrderCreatedAt!.Value,
			TimeSpan.FromMilliseconds(1));
		orders[0].OrderCompletedAt.Should().BeCloseTo(
			newest.OrderCompletedAt!.Value,
			TimeSpan.FromMilliseconds(1));

		var secondPage = await service.GetDisputeOrdersAsync(
			new KeysetPaginationRequest(Cursor: result.NextCursor, PageSize: 2),
			CancellationToken.None);

		secondPage.TotalCount.Should().BeNull();
		secondPage.Items.Select(order => order.EmailInvitationID)
			.Should().Equal(oldest.EmailInvitationID);
		secondPage.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldSearchEligibleOrdersCaseInsensitively()
	{
		// Arrange
		SetAuthenticatedUser(AuthenticatedUserId, AtsRoleIds.User, clientId: 7);
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
		foreach (var order in new[] { firstNameMatch, lastNameMatch, emailMatch, nonMatch, expiredMatch })
		{
			order.ClientId = 7;
			order.RequestorId = AuthenticatedUserId;
		}

		await AddOrdersAsync(firstNameMatch, lastNameMatch, emailMatch, nonMatch, expiredMatch);
		var service = CreateService(CreateSuccessfulEmailService());

		// Act
		var result = await service.GetDisputeOrdersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "needle"),
			CancellationToken.None);

		// Assert
		result.TotalCount.Should().Be(3);
		result.Items.Select(order => order.EmailInvitationID).Should().BeEquivalentTo([
			firstNameMatch.EmailInvitationID,
			lastNameMatch.EmailInvitationID,
			emailMatch.EmailInvitationID
		]);
	}

	[Theory]
	[InlineData(AtsRoleIds.PlatformManager)]
	[InlineData(AtsRoleIds.Admin)]
	public async Task GetDisputeOrdersAsync_ShouldIncludeAllRequestersForAssignedClients(
		int roleId)
	{
		var userId = Guid.CreateVersion7();
		var now = DateTime.UtcNow;
		var assigned = CreateOrder(
			"Assigned",
			"Requester",
			"assigned@example.com",
			now.AddDays(-2),
			now.AddDays(-1));
		assigned.ClientId = 3;
		assigned.RequestorId = Guid.CreateVersion7();
		var sameClient = CreateOrder(
			"SameClient",
			"Requester",
			"same@example.com",
			now.AddDays(-3),
			now.AddDays(-2));
		sameClient.ClientId = 3;
		sameClient.RequestorId = Guid.CreateVersion7();
		var unassigned = CreateOrder(
			"Unassigned",
			"Requester",
			"unassigned@example.com",
			now.AddDays(-4),
			now.AddDays(-3));
		unassigned.ClientId = 4;
		unassigned.RequestorId = userId;
		await AddOrdersAsync(assigned, sameClient, unassigned);
		await AddAssignmentAsync(userId, clientId: 3);
		SetAuthenticatedUser(userId, roleId, clientId: 99);
		var service = CreateService(CreateSuccessfulEmailService());

		var result = await service.GetDisputeOrdersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(2);
		result.Items.Select(order => order.EmailInvitationID)
			.Should().BeEquivalentTo(new[]
			{
				assigned.EmailInvitationID,
				sameClient.EmailInvitationID
			});
	}

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetDisputeOrdersAsync_ShouldRequireOwnRequestorAndClientForRestrictedRoles(
		int roleId)
	{
		var userId = Guid.CreateVersion7();
		var now = DateTime.UtcNow;
		var matching = CreateOrder(
			"Matching",
			"Candidate",
			"matching@example.com",
			now.AddDays(-2),
			now.AddDays(-1));
		matching.ClientId = 5;
		matching.RequestorId = userId;
		var wrongRequester = CreateOrder(
			"WrongRequester",
			"Candidate",
			"wrong-requester@example.com",
			now.AddDays(-3),
			now.AddDays(-2));
		wrongRequester.ClientId = 5;
		wrongRequester.RequestorId = Guid.CreateVersion7();
		var wrongClient = CreateOrder(
			"WrongClient",
			"Candidate",
			"wrong-client@example.com",
			now.AddDays(-4),
			now.AddDays(-3));
		wrongClient.ClientId = 6;
		wrongClient.RequestorId = userId;
		await AddOrdersAsync(matching, wrongRequester, wrongClient);
		SetAuthenticatedUser(userId, roleId, clientId: 5);
		var service = CreateService(CreateSuccessfulEmailService());

		var result = await service.GetDisputeOrdersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(1);
		result.Items.Should().ContainSingle()
			.Which.EmailInvitationID.Should().Be(matching.EmailInvitationID);
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldIncludeAllClientsAndRequesters_ForPlatformSuperAdmin()
	{
		var now = DateTime.UtcNow;
		var first = CreateOrder(
			"FirstClient",
			"Candidate",
			"first-client@example.com",
			now.AddDays(-4),
			now.AddDays(-3));
		first.ClientId = 1;
		first.RequestorId = Guid.CreateVersion7();
		var second = CreateOrder(
			"SecondClient",
			"Candidate",
			"second-client@example.com",
			now.AddDays(-4),
			now.AddDays(-3));
		second.ClientId = 2;
		second.RequestorId = Guid.CreateVersion7();
		await AddOrdersAsync(first, second);
		SetAuthenticatedUser(
			Guid.CreateVersion7(),
			AtsRoleIds.User,
			clientId: 99,
			isPlatformSuperAdmin: true);
		var service = CreateService(CreateSuccessfulEmailService());

		var result = await service.GetDisputeOrdersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(2);
		result.Items.Select(order => order.EmailInvitationID)
			.Should().BeEquivalentTo(new[] { first.EmailInvitationID, second.EmailInvitationID });
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
		order.RequestorId = AuthenticatedUserId;
		await AddOrdersAsync(order);
		await AddAssignmentAsync(AuthenticatedUserId, clientId: 7);

		const string requestor = "requestor@example.com";
		SetAuthenticatedUser(
			AuthenticatedUserId,
			AtsRoleIds.User,
			clientId: 7,
			email: requestor);

		var emailService = CreateSuccessfulEmailService();
		var service = CreateService(emailService);
		var pagination = new KeysetPaginationRequest(Cursor: null, PageSize: 10);
		var cachedBeforeUpdate = await service.GetDisputeOrdersAsync(
			pagination,
			CancellationToken.None);
		cachedBeforeUpdate.Items.Should().ContainSingle().Which.DisputeCategory.Should().BeNull();

		var request = new DisputeOrderRequestDTO
		{
			EmailInvitationId = order.EmailInvitationID,
			DisputeReason = "Report"
		};
		var startedAt = DateTime.UtcNow;

		// Act
		var result = await service.MarkAsDisputedAsync(
			request,
			AuthenticatedUserId,
			CancellationToken.None);

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
		refreshed.Items.Should().ContainSingle().Which.DisputeCategory.Should().Be("Report");

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
		await AddOrdersAsync(order);
		await AddAssignmentAsync(AuthenticatedUserId, clientId: 7);

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
		Func<Task> act = () => service.MarkAsDisputedAsync(
			request,
			AuthenticatedUserId,
			CancellationToken.None);

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
		await AddAssignmentAsync(AuthenticatedUserId, clientId: 7);

		var emailService = CreateSuccessfulEmailService();
		var service = CreateService(emailService);
		var request = CreateDisputeRequest(order);
		using var cancellationSource = new CancellationTokenSource();
		cancellationSource.Cancel();

		// Act
		Func<Task> act = () => service.MarkAsDisputedAsync(
			request,
			AuthenticatedUserId,
			cancellationSource.Token);

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
		var orderHistoryService = new Mock<IOrderHistoryService>();
		var userClientRepository = new Mock<IUserClientRepository>();
		userClientRepository
			.Setup(repository => repository.GetUserClientAssignmentsAsync(
				It.IsAny<IReadOnlyCollection<Guid>>(),
				It.IsAny<CancellationToken>()))
			.Returns<IReadOnlyCollection<Guid>, CancellationToken>(
				async (userIds, cancellationToken) =>
					await _dbContext.UserClientDetails
						.AsNoTracking()
						.Where(assignment => userIds.Contains(assignment.UserId))
						.Select(assignment => new UserClientDetailsDTO
						{
							UserId = assignment.UserId,
							ClientId = assignment.ClientId,
							ClientName = CompanyName
						})
						.ToListAsync(cancellationToken));
		var currentUser = new Mock<ICurrentUser>();
		currentUser.SetupGet(user => user.IsAuthenticated)
			.Returns(() => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true);
		currentUser.SetupGet(user => user.UserId).Returns(() =>
		{
			var value = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return Guid.TryParse(value, out var userId) ? userId : null;
		});
		currentUser.SetupGet(user => user.AtsRoleId).Returns(() =>
		{
			var value = _httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.AtsRoleId)?.Value;
			return int.TryParse(value, out var roleId) ? roleId : null;
		});
		currentUser.SetupGet(user => user.AtsClientId).Returns(() =>
		{
			var value = _httpContextAccessor.HttpContext?.User.FindFirst(AuthClaimTypes.AtsClientId)?.Value;
			return int.TryParse(value, out var clientId) ? clientId : null;
		});
		currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(() =>
			_httpContextAccessor.HttpContext?.User
				.FindAll(AuthClaimTypes.PlatformRoleId)
				.Any(claim => claim.Value == PlatformRoleIds.SuperAdmin.ToString()) == true);

		// A real resolver over the same claims-backed ICurrentUser, so the integration
		// test still exercises the role ladder end to end rather than stubbing it out.
		var accessScopeResolver = new AtsAccessScopeResolver(
			currentUser.Object,
			userClientRepository.Object);

		return new DisputeOrderService(
			NullLogger<DisputeOrderService>.Instance,
			emailService.Object,
			_configuration,
			_atsRepository,
			userClientRepository.Object,
			_httpContextAccessor,
			orderHistoryService.Object,
			currentUser.Object,
			accessScopeResolver,
			new UnitOfWork(_dbContext));
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
		_dbContext.ChangeTracker.Clear();
	}

	private void SetAuthenticatedUser(
		Guid userId,
		int roleId,
		int clientId,
		string? email = null,
		bool isPlatformSuperAdmin = false)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString()),
			new(AuthClaimTypes.AtsClientId, clientId.ToString())
		};
		if (!string.IsNullOrWhiteSpace(email))
			claims.Add(new Claim(ClaimTypes.Email, email));
		if (isPlatformSuperAdmin)
		{
			claims.Add(new Claim(
				AuthClaimTypes.PlatformRoleId,
				PlatformRoleIds.SuperAdmin.ToString()));
		}

		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity(claims, "TestAuth"));
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
			PackageId = DefaultPackageId,
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
