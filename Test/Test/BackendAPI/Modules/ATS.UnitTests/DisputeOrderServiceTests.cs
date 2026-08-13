using System.Security.Claims;
using ATS.Data.Entities;
using ATS.Data.Repository;
using ATS.Data.Repository.Administration.UserClient;
using ATS.Data.Repository.Administration.Clients;
using ATS.DTO;
using ATS.Services;
using ATS.Services.OrderHistory;
using ATS.Constants;
using ATS.Shared.Implementations;
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
	private readonly Mock<IClientRepository> _clientRepository = new();
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();
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
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(false);
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.UserId).Returns(AuthenticatedUserId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(AtsRoleIds.User);

		_service = new DisputeOrderService(
			_logger.Object,
			_emailService.Object,
			configuration,
			_repository.Object,
			_clientRepository.Object,
			_httpContextAccessor,
			new AtsQueryScopeResolver(
				_currentUser.Object,
				_userClientRepository.Object),
			_orderHistoryService.Object);
	}

	#region Happy Path

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldUseUnfilteredRepositoryQuery_WhenSearchTermIsEmpty()
	{
		// Arrange
		var request = new PaginationRequest(PageIndex: 1, PageSize: 10);
		var cancellationToken = new CancellationTokenSource().Token;
		var expected = CreatePaginatedResult();

		_repository
			.Setup(repository => repository.GetDisputeOrdersAsync(
				request, AtsQueryScope.ForRequestor(AuthenticatedUserId), cancellationToken))
			.ReturnsAsync(expected);

		// Act
		var result = await _service.GetDisputeOrdersAsync(request, cancellationToken);

		// Assert
		result.Should().BeSameAs(expected);
		_repository.Verify(
			repository => repository.GetDisputeOrdersAsync(
				request, AtsQueryScope.ForRequestor(AuthenticatedUserId), cancellationToken),
			Times.Once);
		_repository.Verify(
			repository => repository.SearchDisputeOrdersAsync(
				It.IsAny<PaginationRequest>(),
				It.IsAny<AtsQueryScope>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldUseSearchRepositoryQuery_WhenSearchTermIsProvided()
	{
		// Arrange
		var request = new PaginationRequest(PageIndex: 2, PageSize: 5, SearchTerm: "ada");
		var cancellationToken = new CancellationTokenSource().Token;
		var expected = CreatePaginatedResult(pageIndex: 2, pageSize: 5);

		_repository
			.Setup(repository => repository.SearchDisputeOrdersAsync(
				request, AtsQueryScope.ForRequestor(AuthenticatedUserId), cancellationToken))
			.ReturnsAsync(expected);

		// Act
		var result = await _service.GetDisputeOrdersAsync(request, cancellationToken);

		// Assert
		result.Should().BeSameAs(expected);
		_repository.Verify(
			repository => repository.SearchDisputeOrdersAsync(
				request, AtsQueryScope.ForRequestor(AuthenticatedUserId), cancellationToken),
			Times.Once);
		_repository.Verify(
			repository => repository.GetDisputeOrdersAsync(
				It.IsAny<PaginationRequest>(),
				It.IsAny<AtsQueryScope>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldUseAllScope_ForPlatformSuperAdmin()
	{
		var request = new PaginationRequest(PageIndex: 1, PageSize: 10);
		var expected = CreatePaginatedResult();
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(true);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(99);
		_repository.Setup(repository => repository.GetDisputeOrdersAsync(
			request, AtsQueryScope.All, CancellationToken.None)).ReturnsAsync(expected);

		var result = await _service.GetDisputeOrdersAsync(request, CancellationToken.None);

		result.Should().BeSameAs(expected);
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldUseClientScope_ForAtsRoleTwo()
	{
		var request = new PaginationRequest(PageIndex: 1, PageSize: 10);
		var expected = CreatePaginatedResult();
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(false);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(AtsRoleIds.Admin);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(42);
		_userClientRepository.Setup(repository => repository.GetUserClientAssignmentsAsync(
			It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(AuthenticatedUserId)),
			CancellationToken.None)).ReturnsAsync([
				new UserClientDetailsDTO { UserId = AuthenticatedUserId, ClientId = 42 }
			]);
		_repository.Setup(repository => repository.GetDisputeOrdersAsync(
			request, AtsQueryScope.ForClient(42), CancellationToken.None)).ReturnsAsync(expected);

		var result = await _service.GetDisputeOrdersAsync(request, CancellationToken.None);

		result.Should().BeSameAs(expected);
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldUseAssignedClients_ForPlatformManager()
	{
		var managerId = Guid.CreateVersion7();
		var request = new PaginationRequest(PageIndex: 1, PageSize: 10);
		var expected = CreatePaginatedResult();
		_currentUser.SetupGet(user => user.UserId).Returns(managerId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(AtsRoleIds.PlatformManager);
		_userClientRepository.Setup(repository => repository.GetUserClientAssignmentsAsync(
			It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(managerId)),
			CancellationToken.None)).ReturnsAsync([
				new UserClientDetailsDTO { UserId = managerId, ClientId = 1 },
				new UserClientDetailsDTO { UserId = managerId, ClientId = 3 }
			]);
		_repository.Setup(repository => repository.GetDisputeOrdersAsync(
			request,
			It.Is<AtsQueryScope>(scope => scope.Kind == AtsQueryScopeKind.Clients
				&& scope.ClientIds.SequenceEqual(new[] { 1, 3 })),
			CancellationToken.None)).ReturnsAsync(expected);

		var result = await _service.GetDisputeOrdersAsync(request, CancellationToken.None);

		result.Should().BeSameAs(expected);
	}

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetDisputeOrdersAsync_ShouldUseAuthenticatedUserIdAsRequestor_ForUserAndUploader(int roleId)
	{
		var userId = Guid.CreateVersion7();
		var request = new PaginationRequest(PageIndex: 1, PageSize: 10);
		var expected = CreatePaginatedResult();
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(roleId);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(999);
		_repository.Setup(repository => repository.GetDisputeOrdersAsync(
			request, AtsQueryScope.ForRequestor(userId), CancellationToken.None)).ReturnsAsync(expected);

		var result = await _service.GetDisputeOrdersAsync(request, CancellationToken.None);

		result.Should().BeSameAs(expected);
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldUseRequestorScope_ForOtherRoles()
	{
		var requestorId = Guid.CreateVersion7();
		var request = new PaginationRequest(PageIndex: 1, PageSize: 10);
		var expected = CreatePaginatedResult();
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(AtsRoleIds.User);
		_currentUser.SetupGet(user => user.UserId).Returns(requestorId);
		_repository.Setup(repository => repository.GetDisputeOrdersAsync(
			request, AtsQueryScope.ForRequestor(requestorId), CancellationToken.None)).ReturnsAsync(expected);

		var result = await _service.GetDisputeOrdersAsync(request, CancellationToken.None);

		result.Should().BeSameAs(expected);
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldReturnEmptyPage_WhenScopeCannotBeResolved()
	{
		var request = new PaginationRequest(PageIndex: 2, PageSize: 25);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(99);
		_currentUser.SetupGet(user => user.UserId).Returns((Guid?)null);

		var result = await _service.GetDisputeOrdersAsync(request, CancellationToken.None);

		result.PageIndex.Should().Be(2);
		result.PageSize.Should().Be(25);
		result.Count.Should().Be(0);
		result.Data.Should().BeEmpty();
		_repository.Verify(repository => repository.GetDisputeOrdersAsync(
			It.IsAny<PaginationRequest>(), It.IsAny<AtsQueryScope>(), It.IsAny<CancellationToken>()), Times.Never);
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
		var result = await _service.MarkAsDisputedAsync(request, cancellationToken);

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
		var result = await _service.MarkAsDisputedAsync(request, CancellationToken.None);

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
		Func<Task> act = () => _service.MarkAsDisputedAsync(request, CancellationToken.None);

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
		Func<Task> act = () => _service.MarkAsDisputedAsync(request, CancellationToken.None);

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
	public async Task MarkAsDisputedAsync_ShouldRejectOrderOutsideAuthenticatedScope()
	{
		var request = CreateDisputeRequest();
		var order = CreateOrder(request.EmailInvitationId);
		order.RequestorId = Guid.CreateVersion7();
		_repository.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
			request.EmailInvitationId,
			CancellationToken.None)).ReturnsAsync(order);

		Func<Task> act = () => _service.MarkAsDisputedAsync(request, CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>();
		_emailService.Verify(service => service.SendATSEmailAsync(
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		_repository.Verify(repository => repository.MarkAsDisputedAsync(
			It.IsAny<DisputeOrderRequestDTO>(), It.IsAny<CancellationToken>()), Times.Never);
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
		Func<Task> act = () => _service.MarkAsDisputedAsync(request, CancellationToken.None);

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
		Func<Task> act = () => _service.MarkAsDisputedAsync(request, CancellationToken.None);

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
		_clientRepository
			.Setup(repository => repository.GetClientAsync(
				7,
				cancellationToken))
			.ReturnsAsync([
				new ClientDetails { ClientId = 7, ClientName = CompanyName }
			]);
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
		ClientId = 7,
		RequestorId = AuthenticatedUserId,
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

	private static PaginatedResult<DisputeOrderListDTO> CreatePaginatedResult(
		int pageIndex = 1,
		int pageSize = 10)
	{
		var order = new DisputeOrderListDTO
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Ada",
			LastName = "Lovelace",
			OrderCreatedAt = DateTime.UtcNow.AddDays(-2),
			OrderCompletedAt = DateTime.UtcNow.AddDays(-1)
		};

		return new PaginatedResult<DisputeOrderListDTO>(
			pageIndex,
			pageSize,
			1,
			[order]);
	}
}
