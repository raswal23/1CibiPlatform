using ATS.Constants;
using ATS.Data.DTO;
using ATS.Data.Repository.OMSTicketing;
using ATS.Services.AccessScope;
using ATS.Services.OMSTicketingMonitoring;
using ATS.Services.OrderHistory;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class OMSTicketingMonitoringServiceTests
{
	private const int ClientId = 42;

	// ATS.Constants.OrderStatus is internal to the module, so the stored value is used
	// directly here.
	private const string InProgressStatus = "In Progress";

	private static readonly Guid RequestorId = Guid.CreateVersion7();
	private static readonly Guid OrderId = Guid.CreateVersion7();

	private readonly Mock<IOMSTicketingRepository> _repository = new();
	private readonly Mock<IAtsAccessScopeResolver> _scopeResolver = new();
	private readonly Mock<IOrderHistoryService> _orderHistory = new();
	private readonly OMSTicketingMonitoringService _service;

	public OMSTicketingMonitoringServiceTests()
	{
		_scopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AtsAccessScope([ClientId], RequestorId));

		GivenOrder(new TicketRetryTargetDTO
		{
			EmailInvitationID = OrderId,
			ClientId = ClientId,
			RequestorId = RequestorId,
			OrderStatus = InProgressStatus
		});

		_repository
			.Setup(repository => repository.RequeueExhaustedTicketAsync(OrderId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		_service = new OMSTicketingMonitoringService(
			NullLogger<OMSTicketingMonitoringService>.Instance,
			_repository.Object,
			_scopeResolver.Object,
			_orderHistory.Object);
	}

	private void GivenOrder(TicketRetryTargetDTO? target) =>
		_repository
			.Setup(repository => repository.GetRetryTargetAsync(OrderId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(target);

	[Fact]
	public async Task RetryTicketAsync_ShouldRequeueTheOrder_WhenTheCallerOwnsIt()
	{
		var result = await _service.RetryTicketAsync(OrderId, CancellationToken.None);

		result.Should().BeTrue();

		_repository.Verify(
			repository => repository.RequeueExhaustedTicketAsync(OrderId, It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task RetryTicketAsync_ShouldRecordWhoForcedTheRetry()
	{
		await _service.RetryTicketAsync(OrderId, CancellationToken.None);

		// The order's own status is unchanged - a ticket retry is not a step in the
		// order lifecycle - so it is written on both sides of the history entry.
		_orderHistory.Verify(
			history => history.RecordAsync(
				OrderId,
				OrderHistoryEventType.TicketRetryRequested,
				InProgressStatus,
				InProgressStatus,
				It.IsAny<CancellationToken>(),
				It.IsAny<string>()),
			Times.Once);
	}

	[Fact]
	public async Task RetryTicketAsync_ShouldThrowForbidden_WhenTheCallerHasNoATSAccess()
	{
		_scopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync((AtsAccessScope?)null);

		var act = async () => await _service.RetryTicketAsync(OrderId, CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>();
	}

	[Fact]
	public async Task RetryTicketAsync_ShouldThrowNotFound_WhenTheOrderDoesNotExist()
	{
		GivenOrder(null);

		var act = async () => await _service.RetryTicketAsync(OrderId, CancellationToken.None);

		await act.Should().ThrowAsync<NotFoundException>();
	}

	[Fact]
	public async Task RetryTicketAsync_ShouldThrowNotFound_WhenTheOrderBelongsToAnotherClient()
	{
		GivenOrder(new TicketRetryTargetDTO
		{
			EmailInvitationID = OrderId,
			ClientId = ClientId + 1,
			RequestorId = RequestorId,
			OrderStatus = InProgressStatus
		});

		var act = async () => await _service.RetryTicketAsync(OrderId, CancellationToken.None);

		// Not found rather than forbidden: the response must not reveal that an order
		// outside the caller's scope exists.
		await act.Should().ThrowAsync<NotFoundException>();

		_repository.Verify(
			repository => repository.RequeueExhaustedTicketAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task RetryTicketAsync_ShouldThrowNotFound_WhenTheOrderWasRaisedByAnotherUser()
	{
		GivenOrder(new TicketRetryTargetDTO
		{
			EmailInvitationID = OrderId,
			ClientId = ClientId,
			RequestorId = Guid.CreateVersion7(),
			OrderStatus = InProgressStatus
		});

		var act = async () => await _service.RetryTicketAsync(OrderId, CancellationToken.None);

		await act.Should().ThrowAsync<NotFoundException>();
	}

	[Fact]
	public async Task RetryTicketAsync_ShouldThrowConflict_WhenTheOrderIsNoLongerRetryable()
	{
		_repository
			.Setup(repository => repository.RequeueExhaustedTicketAsync(OrderId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

		var act = async () => await _service.RetryTicketAsync(OrderId, CancellationToken.None);

		// A stale button must produce a clear 409, not a silent success.
		await act.Should().ThrowAsync<ConflictException>();

		_orderHistory.Verify(
			history => history.RecordAsync(
				It.IsAny<Guid>(),
				It.IsAny<string>(),
				It.IsAny<string?>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<string>()),
			Times.Never);
	}

	[Fact]
	public async Task RetryTicketAsync_ShouldAllowASuperAdmin_ToRetryAnyOrder()
	{
		// A null client set means unrestricted.
		_scopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AtsAccessScope(null, null));

		GivenOrder(new TicketRetryTargetDTO
		{
			EmailInvitationID = OrderId,
			ClientId = ClientId + 99,
			RequestorId = Guid.CreateVersion7(),
			OrderStatus = InProgressStatus
		});

		var result = await _service.RetryTicketAsync(OrderId, CancellationToken.None);

		result.Should().BeTrue();
	}
}
