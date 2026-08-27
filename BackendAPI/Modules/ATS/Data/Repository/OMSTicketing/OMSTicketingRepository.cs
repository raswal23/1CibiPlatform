namespace ATS.Data.Repository.OMSTicketing;

// Deliberately NOT cached, and no ATSCacheRepository decorator: TicketStatus is as
// volatile as the bulk file status - a Pending order becomes Done within one 10-second
// Quartz tick - so a cached page would show exactly the staleness this screen exists
// to remove. Same reasoning as BulkUploadRepository.
public sealed class OMSTicketingRepository : IOMSTicketingRepository
{
	// An order is retried until this many failed attempts, then it stays Error for a
	// human to look at. A permanently unticketable order must not consume an OMS
	// round trip every tick forever. Public so the UI can say "5/5" and explain why a
	// row stopped retrying, rather than duplicating the number.
	public const int MaxTicketAttempts = 5;

	// Round-robin: each client may contribute at most this many orders per tick, so one
	// large bulk upload cannot block every other client behind it.
	private const int PerClientSliceSize = 30;

	// Ticketing calls a remote legacy database per row, so the batch is smaller than
	// the email queue's 100.
	private const int ClaimBatchSize = 50;

	private readonly ATSDBContext _dbContext;

	public OMSTicketingRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public async Task<List<EmailInvitationRequest>> ClaimPendingTicketsAsync(
		CancellationToken cancellationToken)
	{
		// Claim and return in one statement. FOR UPDATE SKIP LOCKED lets a concurrent
		// worker step over rows another worker is already claiming instead of blocking,
		// and the Processing write is what keeps the claim after this transaction ends.
		// EF cannot express SKIP LOCKED, so this is raw SQL.
		return await _dbContext.EmailInvitationRequests
			.FromSqlRaw(
				"""
				WITH ranked AS (
					SELECT "EmailInvitationID",
						   ROW_NUMBER() OVER (
							   PARTITION BY "ClientId"
							   ORDER BY "OrderCreatedAt") AS rn
					FROM ats."EmailInvitationRequest"
					WHERE "IsTicketed" = false
					  AND ("TicketStatus" = {2}
						OR ("TicketStatus" = {3} AND "TicketAttempts" < {4}))
				)
				UPDATE ats."EmailInvitationRequest" t
				SET "TicketStatus" = {0},
					"TicketClaimedAt" = {1}
				WHERE t."EmailInvitationID" IN (
					SELECT e."EmailInvitationID"
					FROM ats."EmailInvitationRequest" e
					WHERE e."EmailInvitationID" IN (
						SELECT "EmailInvitationID" FROM ranked WHERE rn <= {5})
					ORDER BY e."OrderCreatedAt"
					LIMIT {6}
					FOR UPDATE SKIP LOCKED
				)
				RETURNING t.*;
				""",
				TicketStatus.Processing,
				DateTime.UtcNow,
				TicketStatus.Pending,
				TicketStatus.Error,
				MaxTicketAttempts,
				PerClientSliceSize,
				ClaimBatchSize)
			.AsNoTracking()
			.ToListAsync(cancellationToken);
	}

	public async Task<int> ReleaseStaleTicketClaimsAsync(
		TimeSpan staleAfter,
		CancellationToken cancellationToken)
	{
		var cutoff = DateTime.UtcNow.Subtract(staleAfter);

		return await _dbContext.EmailInvitationRequests
			.Where(x => x.TicketStatus == TicketStatus.Processing
					 && !x.IsTicketed
					 && x.TicketClaimedAt != null
					 && x.TicketClaimedAt < cutoff)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.TicketStatus, x => TicketStatus.Pending)
				.SetProperty(x => x.TicketClaimedAt, x => (DateTime?)null),
				cancellationToken);
	}

	// Left joins throughout: an order claimed at enrolment has no PersonalDetails yet,
	// and a package whose name no longer matches must still come back so the service
	// can park it with a reason rather than silently dropping it from the batch.
	public async Task<List<TicketablePayloadDTO>> GetTicketPayloadsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken)
	{
		if (emailInvitationIds.Count == 0)
		{
			return [];
		}

		var query =
			from invitation in _dbContext.EmailInvitationRequests.AsNoTracking()
			where emailInvitationIds.Contains(invitation.EmailInvitationID)

			from personal in _dbContext.PersonalDetails
				.Where(p => p.EmailInvitationID == invitation.EmailInvitationID)
				.DefaultIfEmpty()

			from package in _dbContext.PackageDetails
				.Where(p => p.PackageName == invitation.SelectPackage)
				.DefaultIfEmpty()

				// UserDetails is keyed (UserId, ModuleId): one row per module grant, each
				// carrying the same Site. Take any one of them.
			from user in _dbContext.UserDetails
				.Where(u => invitation.RequestorId.HasValue && u.UserId == invitation.RequestorId.Value)
				.Take(1)
				.DefaultIfEmpty()

			select new TicketablePayloadDTO
			{
				EmailInvitationID = invitation.EmailInvitationID,
				FirstName = invitation.FirstName,
				MiddleInitial = invitation.MiddleInitial,
				LastName = invitation.LastName,
				EmailAddress = invitation.EmailAddress,
				MobileNumber = invitation.MobileNumber,
				SelectPackage = invitation.SelectPackage,
				RequestorId = invitation.RequestorId,
				DOB = personal != null ? personal.DOB : null,
				PersonalMobileNumber = personal != null ? personal.MobileNumber : null,
				SSS = personal != null ? personal.SSS : null,
				TIN = personal != null ? personal.TIN : null,
				PackageDescription = package != null ? package.PackageDescription : null,

				// Site is ATS-owned. The requestor's name parts are not: UserDetails
				// stores only a joined UserName, so the service resolves those from
				// the Auth directory and fills them in.
				Site = user != null ? user.Site : null,
				RequestorEmail = user != null ? user.UserEmail : null
			};

		return await query.ToListAsync(cancellationToken);
	}

	public async Task<bool> MarkTicketedAsync(
		Guid emailInvitationId,
		string ticketNumber,
		DateTime deliveryDate,
		CancellationToken cancellationToken)
	{
		// The delivery date is read out of SQL Server, which hands it back with
		// Kind=Unspecified. Npgsql refuses to write that into a timestamptz column, so
		// the kind is stamped here rather than at the call site: this is the only place
		// an OMS-sourced date reaches Postgres.
		var deliveryDateUtc = ToUtc(deliveryDate);

		var updated = await _dbContext.EmailInvitationRequests
			.Where(x => x.EmailInvitationID == emailInvitationId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.IsTicketed, x => true)
				.SetProperty(x => x.TicketStatus, x => TicketStatus.Done)
				.SetProperty(x => x.TicketNumber, x => ticketNumber)
				.SetProperty(x => x.TicketDeliveryDate, x => deliveryDateUtc)
				.SetProperty(x => x.TicketClaimedAt, x => (DateTime?)null)
				.SetProperty(x => x.TicketError, x => (string?)null),
				cancellationToken);

		return updated > 0;
	}

	public async Task<int> MarkTicketFailedAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		string reason,
		bool isRetryable,
		CancellationToken cancellationToken)
	{
		if (emailInvitationIds.Count == 0)
		{
			return 0;
		}

		var ids = emailInvitationIds.ToList();

		// Truncated to the column width: the reason is diagnostic text, and an
		// over-long provider message must not fail the write that records it.
		var trimmedReason = reason.Length > 500
			? reason[..500]
			: reason;

		return await _dbContext.EmailInvitationRequests
			.Where(x => ids.Contains(x.EmailInvitationID))
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.TicketStatus, x => TicketStatus.Error)
				.SetProperty(x => x.TicketClaimedAt, x => (DateTime?)null)
				.SetProperty(x => x.TicketError, x => trimmedReason)

				// A non-retryable failure cannot resolve itself, so it consumes the
				// whole budget instead of re-running every tick until it is exhausted.
				.SetProperty(
					x => x.TicketAttempts,
					x => isRetryable ? x.TicketAttempts + 1 : MaxTicketAttempts),
				cancellationToken);
	}

	public async Task<TicketRetryTargetDTO?> GetRetryTargetAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken) =>
		await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(x => x.EmailInvitationID == emailInvitationId)
			.Select(x => new TicketRetryTargetDTO
			{
				EmailInvitationID = x.EmailInvitationID,
				ClientId = x.ClientId,
				RequestorId = x.RequestorId,
				OrderStatus = x.OrderStatus
			})
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<bool> RequeueExhaustedTicketAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken)
	{
		// The predicate is the concurrency guard, not just a lookup: matching on the
		// exhausted state inside the UPDATE means a row the job has already re-claimed,
		// or that another operator retried a moment earlier, updates nothing and the
		// caller is told so. A read-then-write would race and could resurrect a live
		// claim. IsTicketed is checked too - a ticketed order must never re-enter the
		// queue and raise a second ticket in OMS.
		var updated = await _dbContext.EmailInvitationRequests
			.Where(x => x.EmailInvitationID == emailInvitationId
					 && !x.IsTicketed
					 && x.TicketStatus == TicketStatus.Error
					 && x.TicketAttempts >= MaxTicketAttempts)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.TicketStatus, x => TicketStatus.Pending)

				// The budget resets: whatever blocked the order is expected to have been
				// fixed, so the job gets a full set of automatic attempts again.
				.SetProperty(x => x.TicketAttempts, x => 0)
				.SetProperty(x => x.TicketError, x => (string?)null)
				.SetProperty(x => x.TicketClaimedAt, x => (DateTime?)null),
				cancellationToken);

		return updated > 0;
	}

	public async Task<List<TicketedOrderListDTO>> GetTicketedOrdersPageAsync(
		DateTime? afterOrderCreatedAt,
		Guid? afterInvitationId,
		int take,
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var pageQuery = BuildTicketedOrderRowsQuery(
			status,
			searchTerm,
			startDate,
			endDate,
			authorizedClientIds,
			requiredRequestorId);

		if (afterOrderCreatedAt.HasValue && afterInvitationId.HasValue)
		{
			pageQuery = ApplySeek(pageQuery, afterOrderCreatedAt.Value, afterInvitationId.Value);
		}

		return await ApplyOrder(pageQuery)
			.Take(take)
			.ToListAsync(cancellationToken);
	}

	public Task<long> CountTicketedOrdersAsync(
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken) =>
		BuildTicketedOrderRowsQuery(
				status,
				searchTerm,
				startDate,
				endDate,
				authorizedClientIds,
				requiredRequestorId)
			.LongCountAsync(cancellationToken);

	// One round-trip for every bucket. The status filter is deliberately not applied:
	// the chips must keep showing every bucket's size while one is selected.
	public async Task<TicketStatusCountsDTO> GetTicketStatusCountsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var grouped = await BuildTicketedOrderRowsQuery(
				status: null,
				searchTerm,
				startDate,
				endDate,
				authorizedClientIds,
				requiredRequestorId)
			.GroupBy(row => row.TicketStatus)
			.Select(group => new StatusCountRow
			{
				Status = group.Key,
				Count = group.LongCount()
			})
			.ToListAsync(cancellationToken);

		return new TicketStatusCountsDTO
		{
			Pending = CountFor(grouped, TicketStatus.Pending),
			Processing = CountFor(grouped, TicketStatus.Processing),
			Done = CountFor(grouped, TicketStatus.Done),
			Error = CountFor(grouped, TicketStatus.Error),

			// Every row, including any status outside the known vocabulary, so the
			// "All" chip never silently under-reports.
			Total = grouped.Sum(entry => entry.Count)
		};

		static long CountFor(List<StatusCountRow> grouped, string status) =>
			grouped
				.Where(entry => entry.Status == status)
				.Select(entry => entry.Count)
				.FirstOrDefault();
	}

	private sealed class StatusCountRow
	{
		public string? Status { get; set; }

		public long Count { get; set; }
	}

	/// <summary>
	/// Makes a DateTime safe to write to a Postgres timestamptz column. Values coming
	/// back from the legacy OMS SQL Server carry Kind=Unspecified, which Npgsql rejects
	/// outright; they are local Philippine dates, so they are converted rather than
	/// relabelled - stamping them as UTC would shift the date the user sees by the
	/// offset. Values already tagged UTC or Local are converted normally.
	/// </summary>
	private static DateTime ToUtc(DateTime value) =>
		value.Kind switch
		{
			DateTimeKind.Utc => value,
			DateTimeKind.Local => value.ToUniversalTime(),
			_ => TimeZoneInfo.ConvertTimeToUtc(value, OMSTimeZone)
		};

	// The legacy OMS database stores business dates in Philippine local time. The IANA
	// id is tried first so this works on Linux containers as well as Windows.
	private static readonly TimeZoneInfo OMSTimeZone = ResolveOMSTimeZone();

	private static TimeZoneInfo ResolveOMSTimeZone()
	{
		foreach (var id in new[] { "Asia/Manila", "Singapore Standard Time" })
		{
			try
			{
				return TimeZoneInfo.FindSystemTimeZoneById(id);
			}
			catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
			{
				// Try the next id.
			}
		}

		// PHT is UTC+8 with no daylight saving, so a fixed offset is a faithful
		// fallback if neither id is present on the host.
		return TimeZoneInfo.CreateCustomTimeZone("OMS-PHT", TimeSpan.FromHours(8), "OMS PHT", "OMS PHT");
	}

	private IQueryable<TicketedOrderListDTO> BuildTicketedOrderRowsQuery(
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId)
	{
		// Orders enrolled before auto-ticketing shipped have no TicketStatus at all;
		// they were never queued, so they are not part of this screen.
		var query = ApplyOrderScope(
				_dbContext.EmailInvitationRequests.AsNoTracking(),
				authorizedClientIds,
				requiredRequestorId)
			.Where(invitation => invitation.TicketStatus != null);

		if (!string.IsNullOrWhiteSpace(status))
		{
			query = query.Where(invitation => invitation.TicketStatus == status);
		}

		if (startDate.HasValue)
		{
			var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
			query = query.Where(invitation => invitation.OrderCreatedAt >= start);
		}

		if (endDate.HasValue)
		{
			var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
			query = query.Where(invitation => invitation.OrderCreatedAt < end);
		}

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var search = $"%{searchTerm.Trim()}%";
			query = query.Where(invitation =>
				EF.Functions.ILike(invitation.FirstName ?? string.Empty, search)
				|| EF.Functions.ILike(invitation.LastName ?? string.Empty, search)
				|| EF.Functions.ILike(invitation.EmailAddress ?? string.Empty, search)
				|| EF.Functions.ILike(invitation.Requestor ?? string.Empty, search)
				|| EF.Functions.ILike(invitation.SelectPackage ?? string.Empty, search)
				|| EF.Functions.ILike(invitation.TicketNumber ?? string.Empty, search));
		}

		return query.Select(invitation => new TicketedOrderListDTO
		{
			EmailInvitationID = invitation.EmailInvitationID,
			FirstName = invitation.FirstName,
			MiddleInitial = invitation.MiddleInitial,
			LastName = invitation.LastName,
			EmailAddress = invitation.EmailAddress,
			Requestor = invitation.Requestor,
			SelectPackage = invitation.SelectPackage,
			TicketStatus = invitation.TicketStatus,
			TicketNumber = invitation.TicketNumber,
			TicketDeliveryDate = invitation.TicketDeliveryDate,
			TicketAttempts = invitation.TicketAttempts,
			TicketError = invitation.TicketError,
			OrderCreatedAt = invitation.OrderCreatedAt
		});
	}

	// Mirrors BulkUploadRepository.ApplyFileScope: a null client set means unrestricted
	// (super admin), an empty set filters everything out.
	private static IQueryable<EmailInvitationRequest> ApplyOrderScope(
		IQueryable<EmailInvitationRequest> query,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId) =>
		query.Where(invitation => (authorizedClientIds == null
				|| (invitation.ClientId.HasValue && authorizedClientIds.Contains(invitation.ClientId.Value)))
			&& (!requiredRequestorId.HasValue
				|| invitation.RequestorId == requiredRequestorId.Value));

	// Newest order first, unique EmailInvitationID as the tiebreaker. ApplySeek below
	// must mirror this expression exactly. Matches IX (OrderCreatedAt DESC, ID ASC).
	private static IQueryable<TicketedOrderListDTO> ApplyOrder(
		IQueryable<TicketedOrderListDTO> pageQuery) =>
		pageQuery
			.OrderByDescending(row => row.OrderCreatedAt)
			.ThenBy(row => row.EmailInvitationID);

	private static IQueryable<TicketedOrderListDTO> ApplySeek(
		IQueryable<TicketedOrderListDTO> pageQuery,
		DateTime afterOrderCreatedAt,
		Guid afterInvitationId) =>
		pageQuery.Where(row => row.OrderCreatedAt < afterOrderCreatedAt
			|| (row.OrderCreatedAt == afterOrderCreatedAt
				&& row.EmailInvitationID.CompareTo(afterInvitationId) > 0));
}
