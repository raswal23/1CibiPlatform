using System.Security.Claims;
using ATS.Data.Entities;
using ATS.Constants;
using ATS.Services.AccessScope;
using ATS.Data.Repository;
using ATS.Data.UnitOfWork;
using ATS.DTO;
using ATS.Services.DisputeOrder;
using ATS.Services.EmailService;
using ATS.Services.OrderHistory;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class DisputeOrderServiceTests
{
	private const string RequestorEmail = "requestor@cibi.test";
	private const string CompanyName = "Analytical Engines Ltd.";
	private static readonly Guid AuthenticatedUserId = Guid.CreateVersion7();

	private readonly Mock<ILogger<DisputeOrderService>> _logger = new();
	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IUserClientRepository> _userClientRepository = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly Mock<IAtsAccessScopeResolver> _accessScopeResolver = new();
	private readonly Mock<IUnitOfWork> _unitOfWork = new();

	// The requestor-facing acknowledgement. Mocked because these tests are about the service's
	// orchestration - when the notice is sent, and when it must not be. Its own behaviour,
	// including that it swallows delivery failures, is covered in DisputeEmailNotificationTests.
	private readonly Mock<IDisputeEmailNotification> _disputeEmailNotification = new();
	private readonly HttpContextAccessor _httpContextAccessor;
	private readonly DisputeOrderService _service;

	public DisputeOrderServiceTests()
	{
		_httpContextAccessor = new HttpContextAccessor
		{
			HttpContext = CreateHttpContext(new Claim(ClaimTypes.Email, RequestorEmail))
		};

		_service = new DisputeOrderService(
			_logger.Object,
			_repository.Object,
			_userClientRepository.Object,
			_httpContextAccessor,
			_orderHistoryService.Object,
			_currentUser.Object,
			_accessScopeResolver.Object,
			_unitOfWork.Object,
			_disputeEmailNotification.Object);
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
	public async Task GetDisputeOrdersAsync_ShouldEncodeCompletedAtInNextCursor_WhenMoreRowsExist()
	{
		var userId = SetAuthenticatedUser(AtsRoleIds.User, clientId: 7);
		SetAccessScope([7], userId);
		var completedAt = new DateTime(2026, 8, 20, 9, 30, 0, DateTimeKind.Utc);
		var first = new DisputeOrderListDTO
		{
			EmailInvitationID = Guid.CreateVersion7(),
			OrderCompletedAt = completedAt
		};
		var second = new DisputeOrderListDTO
		{
			EmailInvitationID = Guid.CreateVersion7(),
			OrderCompletedAt = completedAt.AddMinutes(-1)
		};

		_repository.Setup(repository => repository.GetDisputeOrdersPageAsync(
			null,
			null,
			null,
			2,
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 7 })),
			userId,
			CancellationToken.None)).ReturnsAsync([first, second]);
		_repository.Setup(repository => repository.CountDisputeOrdersAsync(
			null,
			It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 7 })),
			userId,
			CancellationToken.None)).ReturnsAsync(2);

		var result = await _service.GetDisputeOrdersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 1),
			CancellationToken.None);

		result.Items.Should().ContainSingle().Which.Should().BeSameAs(first);
		result.NextCursor.Should().NotBeNull();
		var fields = CursorCodec.Decode(result.NextCursor, 2);
		fields.Should().NotBeNull();
		fields![0].Should().Be(completedAt.ToString("O"));
		fields[1].Should().Be(first.EmailInvitationID.ToString("D"));
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldMarkTheOrderDisputedAndReturnTrue()
	{
		// Arrange
		var request = CreateDisputeRequest();
		var cancellationToken = new CancellationTokenSource().Token;
		SetupResolvedDisputeContext(request, cancellationToken);
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, cancellationToken))
			.ReturnsAsync(true);

		// Act
		var result = await _service.MarkAsDisputedAsync(request, AuthenticatedUserId, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_repository.Verify(
			repository => repository.MarkAsDisputedAsync(request, cancellationToken),
			Times.Once);
		_unitOfWork.Verify(uow => uow.CommitAsync(cancellationToken), Times.Once);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldAcknowledgeTheFallbackEmailClaim_WhenStandardEmailClaimIsMissing()
	{
		// Arrange: some tokens carry only the short "email" claim rather than ClaimTypes.Email. The
		// acknowledgement still has to reach the filer, so the fallback address is what it goes to.
		const string fallbackEmail = "fallback@cibi.test";
		_httpContextAccessor.HttpContext = CreateHttpContext(new Claim("email", fallbackEmail));

		var request = CreateDisputeRequest();
		SetupResolvedDisputeContext(request, CancellationToken.None);
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, CancellationToken.None))
			.ReturnsAsync(true);

		// Act
		var result = await _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert
		result.Should().BeTrue();
		_disputeEmailNotification.Verify(
			notifier => notifier.SendAsync(
				It.Is<DisputeEmailDetails>(details => details.RequestorEmail == fallbackEmail),
				CancellationToken.None),
			Times.Once);
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
		VerifyNotAcknowledged();
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
		VerifyNotAcknowledged();
		_repository.Verify(
			repository => repository.MarkAsDisputedAsync(
				It.IsAny<DisputeOrderRequestDTO>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldWrapRepositoryFailure_AndNotAcknowledgeTheFiler()
	{
		// Arrange
		var request = CreateDisputeRequest();
		SetupResolvedDisputeContext(request, CancellationToken.None);
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, CancellationToken.None))
			.ThrowsAsync(new InvalidOperationException("Database unavailable."));

		// Act
		Func<Task> act = () => _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert: the failure is wrapped and the transaction rolled back, and nothing is
		// acknowledged - confirming a dispute that was never recorded would leave the filer waiting
		// on something that does not exist.
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("Failed to mark order as disputed.");
		_repository.Verify(
			repository => repository.MarkAsDisputedAsync(request, CancellationToken.None),
			Times.Once);
		_unitOfWork.Verify(uow => uow.RollbackAsync(CancellationToken.None), Times.Once);
		VerifyNotAcknowledged();
	}

	#endregion

	#region Requestor Acknowledgement

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldAcknowledgeTheFilerAfterTheCommit()
	{
		// Arrange
		var request = CreateDisputeRequest();
		SetupResolvedDisputeContext(request, CancellationToken.None);
		_currentUser.SetupGet(user => user.FullName).Returns("Ana Reyes");
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, CancellationToken.None))
			.ReturnsAsync(true);

		// Act
		var result = await _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert: the acknowledgement goes to whoever FILED the dispute, named from the token, and
		// carries the candidate the dispute is about plus the order it belongs to - the id is what
		// the notice records itself against in that order's history. It is a separate message from
		// the operations notification asserted in the happy-path tests above.
		result.Should().BeTrue();
		_disputeEmailNotification.Verify(
			notifier => notifier.SendAsync(
				It.Is<DisputeEmailDetails>(details =>
					details.EmailInvitationId == request.EmailInvitationId
					&& details.RequestorEmail == RequestorEmail
					&& details.RequestorName == "Ana Reyes"
					&& details.CandidateName == "Ada Lovelace"
					&& details.DisputeCategory == "Report"
					&& details.DisputeReason == "The employment dates on the report are wrong."),
				CancellationToken.None),
			Times.Once);
		_unitOfWork.Verify(uow => uow.CommitAsync(CancellationToken.None), Times.Once);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldPassCategoryAndReasonThroughUnmangled()
	{
		// Arrange: the console sends the label as the category and the typed description as the
		// reason, for every category. Deciding how the two become body lines is the notifier's job,
		// not this service's, so both have to arrive here unchanged.
		var request = new DisputeOrderRequestDTO
		{
			EmailInvitationId = Guid.CreateVersion7(),
			DisputeCategory = "Others",
			DisputeReason = "The report lists an employer I never worked for."
		};
		SetupResolvedDisputeContext(request, CancellationToken.None);
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, CancellationToken.None))
			.ReturnsAsync(true);

		// Act
		await _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert
		_disputeEmailNotification.Verify(
			notifier => notifier.SendAsync(
				It.Is<DisputeEmailDetails>(details =>
					details.DisputeCategory == "Others"
					&& details.DisputeReason == "The report lists an employer I never worked for."),
				CancellationToken.None),
			Times.Once);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldFallBackToTheCandidateAddress_WhenTheOrderHasNoName()
	{
		// Arrange: the copy reads "A dispute has been submitted for <candidate>", so an order whose
		// name parts were never filled in still has to identify someone.
		var request = CreateDisputeRequest();
		var order = SetupResolvedDisputeContext(request, CancellationToken.None);
		order.FirstName = null;
		order.LastName = null;
		order.EmailAddress = "candidate@example.test";
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, CancellationToken.None))
			.ReturnsAsync(true);

		// Act
		await _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert
		_disputeEmailNotification.Verify(
			notifier => notifier.SendAsync(
				It.Is<DisputeEmailDetails>(details => details.CandidateName == "candidate@example.test"),
				CancellationToken.None),
			Times.Once);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldNotAcknowledgeTheFiler_WhenTheDisputeWriteFails()
	{
		// Arrange
		var request = CreateDisputeRequest();
		SetupResolvedDisputeContext(request, CancellationToken.None);
		_repository
			.Setup(repository => repository.MarkAsDisputedAsync(request, CancellationToken.None))
			.ThrowsAsync(new InvalidOperationException("Database unavailable."));

		// Act
		Func<Task> act = () => _service.MarkAsDisputedAsync(request, AuthenticatedUserId, CancellationToken.None);

		// Assert: the acknowledgement is strictly after the commit, so nothing is confirmed for a
		// dispute that was rolled back.
		await act.Should().ThrowAsync<InternalServerException>();
		_disputeEmailNotification.Verify(
			notifier => notifier.SendAsync(
				It.IsAny<DisputeEmailDetails>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
		_unitOfWork.Verify(uow => uow.RollbackAsync(CancellationToken.None), Times.Once);
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

	/// <summary>
	/// Asserts no acknowledgement reached the filer. Every path that must not confirm a dispute uses
	/// it: a missing order, a user with no client assignment, and a write that rolled back.
	/// </summary>
	private void VerifyNotAcknowledged() =>
		_disputeEmailNotification.Verify(
			notifier => notifier.SendAsync(
				It.IsAny<DisputeEmailDetails>(),
				It.IsAny<CancellationToken>()),
			Times.Never);

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

		// What the console sends for any dispute: the selected label, plus the description every
		// category now requires.
		DisputeCategory = "Report",
		DisputeReason = "The employment dates on the report are wrong."
	};

	private static List<DisputeOrderListDTO> CreateDisputeOrders() =>
	[
		new DisputeOrderListDTO
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Ada",
			LastName = "Lovelace",
			DisputedAt = DateTime.UtcNow.AddDays(-2),
			OrderCompletedAt = DateTime.UtcNow.AddDays(-1)
		}
	];
}
