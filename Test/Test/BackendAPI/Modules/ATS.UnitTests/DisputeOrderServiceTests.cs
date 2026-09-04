using System.Security.Claims;
using ATS.Data.Entities;
using ATS.Constants;
using ATS.Services.AccessScope;
using ATS.Data.Repository;
using ATS.Data.UnitOfWork;
using ATS.DTO;
using ATS.Services.DisputeOrder;
using ATS.Services.OrderHistory;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using BuildingBlocks.SharedServices.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class DisputeOrderServiceTests
{
	private const string DisputeRecipient = "disputes@cibi.test";
	private const string RequestorEmail = "requestor@cibi.test";
	private const string CompanyName = "Analytical Engines Ltd.";
	private const string EmailBody = "dispute-email-body";
	private static readonly Guid AuthenticatedUserId = Guid.CreateVersion7();

	private readonly Mock<ILogger<DisputeOrderService>> _logger = new();
	private readonly Mock<IEmailService> _emailService = new();
	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IUserClientRepository> _userClientRepository = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly Mock<IAtsAccessScopeResolver> _accessScopeResolver = new();
	private readonly Mock<IUnitOfWork> _unitOfWork = new();
	private readonly HttpContextAccessor _httpContextAccessor;
	private readonly DisputeOrderService _service;

	public DisputeOrderServiceTests()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ATS:DisputeOrderEmailRecipient"] = DisputeRecipient
			})
			.Build();

		_httpContextAccessor = new HttpContextAccessor
		{
			HttpContext = CreateHttpContext(new Claim(ClaimTypes.Email, RequestorEmail))
		};

		_service = new DisputeOrderService(
			_logger.Object,
			_emailService.Object,
			configuration,
			_repository.Object,
			_userClientRepository.Object,
			_httpContextAccessor,
			_orderHistoryService.Object,
			_currentUser.Object,
			_accessScopeResolver.Object,
			_unitOfWork.Object);
	}

	/// <summary>
	/// Sets the scope the service sees. The role ladder itself moved to
	/// AtsAccessScopeResolver and is tested there.
	/// </summary>
	private void SetAccessScope(IReadOnlyCollection<int>? authorizedClientIds, Guid? requiredOwnerId)
	{
		_accessScopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AtsAccessScope(authorizedClientIds, requiredOwnerId));
	}

	#region Happy Path

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldScopeToOwnClientAndRequestor_WhenSearchTermIsEmpty()
	{
		// Arrange
		var userId = SetAuthenticatedUser(AtsRoleIds.User, clientId: 7);
		SetAccessScope([7], userId);
		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 10);
		var cancellationToken = new CancellationTokenSource().Token;
		var rows = CreateDisputeOrders();

		_repository
			.Setup(repository => repository.GetDisputeOrdersPageAsync(
				null,
				null,
				null,
				11,
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 7 })),
				userId,
				cancellationToken))
			.ReturnsAsync(rows.ToList());
		_repository
			.Setup(repository => repository.CountDisputeOrdersAsync(
				null,
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 7 })),
				userId,
				cancellationToken))
			.ReturnsAsync(1);

		// Act
		var result = await _service.GetDisputeOrdersAsync(request, cancellationToken);

		// Assert
		result.Items.Should().BeEquivalentTo(rows);
		result.TotalCount.Should().Be(1);
		_repository.VerifyAll();
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldPassSearchTerm_WhenProvided()
	{
		// Arrange
		// An Admin sees every client assigned to them, with no owner predicate.
		SetAuthenticatedUser(AtsRoleIds.Admin, clientId: 99);
		SetAccessScope([1, 3], null);
		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 5, SearchTerm: "ada");
		var cancellationToken = new CancellationTokenSource().Token;
		var rows = CreateDisputeOrders();

		_repository
			.Setup(repository => repository.GetDisputeOrdersPageAsync(
				"ada",
				null,
				null,
				6,
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 1, 3 })),
				null,
				cancellationToken))
			.ReturnsAsync(rows.ToList());
		_repository
			.Setup(repository => repository.CountDisputeOrdersAsync(
				"ada",
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 1, 3 })),
				null,
				cancellationToken))
			.ReturnsAsync(1);

		// Act
		var result = await _service.GetDisputeOrdersAsync(request, cancellationToken);

		// Assert
		result.Items.Should().BeEquivalentTo(rows);
		result.TotalCount.Should().Be(1);
		_repository.VerifyAll();
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldSendNotificationUpdateRepositoryAndReturnTrue()
	{
		// Arrange
		var request = CreateDisputeRequest();
		var cancellationToken = new CancellationTokenSource().Token;
		var order = SetupResolvedDisputeContext(request, cancellationToken);
		SetupSuccessfulEmail();
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, cancellationToken))
			.ReturnsAsync(true);

		// Act
		var result = await _service.MarkAsDisputedAsync(request, AuthenticatedUserId, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_emailService.Verify(emailService => emailService.SendEmailForDispute(
			DisputeRecipient,
			CompanyName,
			request.DisputeReason!,
			order.OrderCreatedAt,
			RequestorEmail,
			$"{order.FirstName} {order.LastName}"), Times.Once);
		_emailService.Verify(emailService => emailService.SendATSEmailAsync(
			DisputeRecipient,
			"CIBI | Dispute Order Notification",
			EmailBody), Times.Once);
		_repository.Verify(
			repository => repository.MarkAsDisputedAsync(request, cancellationToken),
			Times.Once);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldUseFallbackEmailClaim_WhenStandardEmailClaimIsMissing()
	{
		// Arrange
		const string fallbackEmail = "fallback@cibi.test";
		_httpContextAccessor.HttpContext = CreateHttpContext(new Claim("email", fallbackEmail));

		var request = CreateDisputeRequest();
		var order = SetupResolvedDisputeContext(request, CancellationToken.None);
		SetupSuccessfulEmail();
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, CancellationToken.None))
			.ReturnsAsync(true);

		// Act
		var result = await _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert
		result.Should().BeTrue();
		_emailService.Verify(emailService => emailService.SendEmailForDispute(
			DisputeRecipient,
			CompanyName,
			request.DisputeReason!,
			order.OrderCreatedAt,
			fallbackEmail,
			$"{order.FirstName} {order.LastName}"), Times.Once);
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldRejectMissingOrderBeforeSendingEmail()
	{
		// Arrange
		var request = CreateDisputeRequest();
		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				request.EmailInvitationId,
				CancellationToken.None))
			.ReturnsAsync(new EmailInvitationRequest());

		// Act
		Func<Task> act = () => _service.MarkAsDisputedAsync(
			request,
			AuthenticatedUserId,
			CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage("Email invitation request not found.");
		_emailService.Verify(
			emailService => emailService.SendATSEmailAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()),
			Times.Never);
		_repository.Verify(
			repository => repository.MarkAsDisputedAsync(
				It.IsAny<DisputeOrderRequestDTO>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldRejectUserWithoutClientAssignment()
	{
		// Arrange
		var request = CreateDisputeRequest();
		var order = CreateOrder(request.EmailInvitationId);
		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				request.EmailInvitationId,
				CancellationToken.None))
			.ReturnsAsync(order);
		_userClientRepository
			.Setup(repository => repository.GetUserClientAssignmentsAsync(
				It.IsAny<IReadOnlyCollection<Guid>>(),
				CancellationToken.None))
			.ReturnsAsync(Array.Empty<UserClientDetailsDTO>());

		// Act
		Func<Task> act = () => _service.MarkAsDisputedAsync(
			request,
			AuthenticatedUserId,
			CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("The authenticated user does not have a valid client assignment.");
		_emailService.Verify(
			emailService => emailService.SendATSEmailAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()),
			Times.Never);
		_repository.Verify(
			repository => repository.MarkAsDisputedAsync(
				It.IsAny<DisputeOrderRequestDTO>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldThrowAndSkipRepository_WhenEmailReturnsFalse()
	{
		// Arrange
		var request = CreateDisputeRequest();
		SetupResolvedDisputeContext(request, CancellationToken.None);
		_emailService
			.Setup(emailService => emailService.SendEmailForDispute(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.Returns(EmailBody);
		_emailService
			.Setup(emailService => emailService.SendATSEmailAsync(
				DisputeRecipient,
				"CIBI | Dispute Order Notification",
				EmailBody))
			.ReturnsAsync(false);

		// Act
		Func<Task> act = () => _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("Failed to send dispute order notification email.");
		_repository.Verify(
			repository => repository.MarkAsDisputedAsync(
				It.IsAny<DisputeOrderRequestDTO>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldWrapRepositoryFailure_AfterEmailIsSent()
	{
		// Arrange
		var request = CreateDisputeRequest();
		SetupResolvedDisputeContext(request, CancellationToken.None);
		SetupSuccessfulEmail();
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, CancellationToken.None))
			.ThrowsAsync(new InvalidOperationException("Database unavailable."));

		// Act
		Func<Task> act = () => _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("Failed to mark order as disputed.");
		_emailService.Verify(emailService => emailService.SendATSEmailAsync(
			DisputeRecipient,
			"CIBI | Dispute Order Notification",
			EmailBody), Times.Once);
		_repository.Verify(
			repository => repository.MarkAsDisputedAsync(request, CancellationToken.None),
			Times.Once);
	}

	#endregion

	private Guid SetAuthenticatedUser(int roleId, int clientId)
	{
		var userId = Guid.CreateVersion7();
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(roleId);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(clientId);
		return userId;
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldBypassAllDataFilters_ForPlatformSuperAdmin()
	{
		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 10);
		var rows = CreateDisputeOrders();
		// (null, null) is the super admin scope: no client and no owner predicate.
		SetAuthenticatedUser(AtsRoleIds.User, clientId: 7);
		SetAccessScope(null, null);
		_repository.Setup(repository => repository.GetDisputeOrdersPageAsync(
			null,
			null,
			null,
			11,
			null,
			null,
			CancellationToken.None)).ReturnsAsync(rows.ToList());
		_repository.Setup(repository => repository.CountDisputeOrdersAsync(
			null,
			null,
			null,
			CancellationToken.None)).ReturnsAsync(1);

		var result = await _service.GetDisputeOrdersAsync(request, CancellationToken.None);

		result.Items.Should().BeEquivalentTo(rows);
		_userClientRepository.Verify(repository => repository.GetUserClientAssignmentsAsync(
			It.IsAny<IReadOnlyCollection<Guid>>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	private void SetupSuccessfulEmail()
	{
		_emailService
			.Setup(emailService => emailService.SendEmailForDispute(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.Returns(EmailBody);
		_emailService
			.Setup(emailService => emailService.SendATSEmailAsync(
				DisputeRecipient,
				"CIBI | Dispute Order Notification",
				EmailBody))
			.ReturnsAsync(true);
	}

	private EmailInvitationRequest SetupResolvedDisputeContext(
		DisputeOrderRequestDTO request,
		CancellationToken cancellationToken)
	{
		var order = CreateOrder(request.EmailInvitationId);
		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				request.EmailInvitationId,
				cancellationToken))
			.ReturnsAsync(order);
		_userClientRepository
			.Setup(repository => repository.GetUserClientAssignmentsAsync(
				It.Is<IReadOnlyCollection<Guid>>(userIds =>
					userIds.Count == 1 && userIds.Contains(AuthenticatedUserId)),
				cancellationToken))
			.ReturnsAsync([
				new UserClientDetailsDTO
				{
					UserId = AuthenticatedUserId,
					ClientId = 7,
					ClientName = CompanyName
				}
			]);

		return order;
	}

	private static EmailInvitationRequest CreateOrder(Guid emailInvitationId) => new()
	{
		EmailInvitationID = emailInvitationId,
		FirstName = "Ada",
		LastName = "Lovelace",
		OrderCreatedAt = new DateTime(2026, 8, 1, 8, 30, 0, DateTimeKind.Utc)
	};

	private static DefaultHttpContext CreateHttpContext(Claim emailClaim)
	{
		var context = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(new ClaimsIdentity([emailClaim], "TestAuth"))
		};

		return context;
	}

	private static DisputeOrderRequestDTO CreateDisputeRequest() => new()
	{
		EmailInvitationId = Guid.CreateVersion7(),
		DisputeReason = "Report"
	};

	private static List<DisputeOrderListDTO> CreateDisputeOrders() =>
	[
		new DisputeOrderListDTO
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Ada",
			LastName = "Lovelace",
			OrderCreatedAt = DateTime.UtcNow.AddDays(-2),
			OrderCompletedAt = DateTime.UtcNow.AddDays(-1)
		}
	];
}
