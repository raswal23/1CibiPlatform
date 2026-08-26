namespace ATS.Services.OMSTicketing;

public class OMSTicketingProcessorService : IOMSTicketingProcessorService
{
	// Comfortably longer than a full ticketing pass so a live worker is never robbed of
	// orders it is still processing. Must exceed the worst-case batch duration.
	private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(30);

	// The legacy OMS database runs three stored procedures per ticket, so the fan-out
	// is deliberately narrow.
	private const int MaxDegreeOfParallelism = 3;

	private readonly ILogger<OMSTicketingProcessorService> _logger;
	private readonly IOMSTicketingRepository _repository;
	private readonly IServiceScopeFactory _serviceScopeFactory;

	public OMSTicketingProcessorService(
		ILogger<OMSTicketingProcessorService> logger,
		IOMSTicketingRepository repository,
		IServiceScopeFactory serviceScopeFactory)
	{
		_logger = logger;
		_repository = repository;
		_serviceScopeFactory = serviceScopeFactory;
	}

	public async Task ProcessAsync(CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "OMSTicketing",
			Step = "ProcessPending",
			Timestamp = DateTime.UtcNow
		};

		// A crash mid-call leaves orders claimed as Processing with no live worker, so
		// release anything stale before claiming the next batch.
		var released = await _repository.ReleaseStaleTicketClaimsAsync(
			StaleClaimTimeout,
			cancellationToken);

		if (released > 0)
		{
			_logger.LogWarning(
				"Released {ReleasedCount} stale OMS ticket claim(s) back to Pending.",
				released);
		}

		// The claim atomically moves a batch of orders to Processing, so a concurrent
		// worker cannot raise a second ticket for the same order.
		var claimed = await _repository.ClaimPendingTicketsAsync(cancellationToken);

		if (claimed.Count == 0)
		{
			return;
		}

		var claimedIds = claimed
			.Select(order => order.EmailInvitationID)
			.ToList();

		var payloads = await _repository.GetTicketPayloadsAsync(claimedIds, cancellationToken);

		// A claimed order with no payload row cannot be projected at all. Park it
		// rather than leaving it stuck in Processing until the sweeper releases it.
		var missing = claimedIds
			.Except(payloads.Select(payload => payload.EmailInvitationID))
			.ToList();

		if (missing.Count > 0)
		{
			await _repository.MarkTicketFailedAsync(
				missing,
				"The order details required to build the OMS ticket could not be loaded.",
				isRetryable: false,
				cancellationToken);
		}

		using var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);

		var tasks = payloads.Select(payload => ProcessOneAsync(payload, semaphore, cancellationToken));

		var results = await Task.WhenAll(tasks);

		var succeeded = results.Count(result => result.Succeeded);

		_logger.LogInformation(
			"OMS ticketing processed {SucceededCount} of {ClaimedCount} claimed order(s): {@Context}",
			succeeded,
			claimed.Count,
			logContext);
	}

	private async Task<(Guid Id, bool Succeeded)> ProcessOneAsync(
		TicketablePayloadDTO payload,
		SemaphoreSlim semaphore,
		CancellationToken cancellationToken)
	{
		await semaphore.WaitAsync(cancellationToken);

		// Each order gets its own scope: the repository owns a DbContext, which is not
		// safe to share across the concurrent calls above.
		using var scope = _serviceScopeFactory.CreateScope();

		var repository = scope.ServiceProvider.GetRequiredService<IOMSTicketingRepository>();

		try
		{
			var authQueries = scope.ServiceProvider.GetRequiredService<IAuthQueries>();
			var ticketCreator = scope.ServiceProvider.GetRequiredService<IOMSTicketCreator>();

			// UserDetails only stores a joined display name, so the requestor's name
			// parts come from the Auth directory.
			var requestor = payload.RequestorId.HasValue
				? await authQueries.GetATSAssignedUserAsync(payload.RequestorId.Value, cancellationToken)
				: null;

			var (request, failure) = OMSTicketPayloadMapper.TryMap(
				payload,
				requestor?.FirstName,
				requestor?.LastName);

			if (request is null)
			{
				// Nothing about this order will change on its own, so it is parked
				// without spending an OMS round trip or a retry attempt on it.
				_logger.LogWarning(
					"Order {EmailInvitationID} cannot be ticketed: {Reason}",
					payload.EmailInvitationID,
					failure);

				await repository.MarkTicketFailedAsync(
					[payload.EmailInvitationID],
					failure!,
					isRetryable: false,
					cancellationToken);

				return (payload.EmailInvitationID, false);
			}

			// The invitation id travels as the OMS reference number so the ticket can
			// be tied back to this order, and so a retry after a timeout is
			// recognisable rather than creating a second ticket.
			var ticket = await ticketCreator.CreateTicketAsync(
				request,
				cancellationToken,
				payload.EmailInvitationID.ToString("D"));

			await repository.MarkTicketedAsync(
				payload.EmailInvitationID,
				ticket.TicketNumber,
				ticket.DeliveryDate,
				cancellationToken);

			_logger.LogInformation(
				"Order {EmailInvitationID} ticketed as {TicketNumber}.",
				payload.EmailInvitationID,
				ticket.TicketNumber);

			return (payload.EmailInvitationID, true);
		}
		catch (BadRequestException ex)
		{
			// OMS rejected the request on business grounds - an unknown requestor or an
			// exhausted PO. Retrying cannot fix either, so it needs a human.
			_logger.LogWarning(
				ex,
				"OMS rejected the ticket for order {EmailInvitationID}.",
				payload.EmailInvitationID);

			await repository.MarkTicketFailedAsync(
				[payload.EmailInvitationID],
				ex.Message,
				isRetryable: false,
				cancellationToken);

			return (payload.EmailInvitationID, false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Anything else is treated as transient: the order goes back into the queue
			// and is retried until the attempt cap is reached.
			_logger.LogError(
				ex,
				"Failed to create the OMS ticket for order {EmailInvitationID}, will retry.",
				payload.EmailInvitationID);

			await repository.MarkTicketFailedAsync(
				[payload.EmailInvitationID],
				ex.Message,
				isRetryable: true,
				cancellationToken);

			return (payload.EmailInvitationID, false);
		}
		finally
		{
			semaphore.Release();
		}
	}
}
