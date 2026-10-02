namespace ATS.Services.AuditTrail;

public sealed class AtsAuditService : IAtsAuditService
{
	// What the AI assistant may read in one turn. Higher than the order search ceiling
	// because "list all the failures" is a normal audit question and ten rows reads as a
	// broken answer. Still bounded: the rows are rendered into a chat bubble and summarised
	// by a model, so anything larger belongs in the Excel export.
	private const int MaxAssistantEntries = 50;

	// A failure reason is diagnostic prose; the assistant only needs enough to say what
	// went wrong.
	private const int MaxFailureReasonLength = 200;

	// The trail is append-only and grows without limit, so an export is capped rather than
	// building an unbounded workbook in memory. Comfortably above a month of normal
	// activity, which is all the retention job keeps anyway.
	private const int MaxExportRows = 10_000;

	private readonly IAtsAuditRepository _auditRepository;
	private readonly ICurrentUser _currentUser;
	private readonly ILogger<AtsAuditService> _logger;

	public AtsAuditService(
		IAtsAuditRepository auditRepository,
		ICurrentUser currentUser,
		ILogger<AtsAuditService> logger)
	{
		_auditRepository = auditRepository;
		_currentUser = currentUser;
		_logger = logger;
	}

	public async Task<KeysetPaginatedResult<AuditTrailListDTO>> GetAuditTrailAsync(
		KeysetPaginationRequest paginationRequest,
		string? outcome,
		string? action,
		string? area,
		CancellationToken cancellationToken)
	{
		if (!CanRead())
		{
			return new KeysetPaginatedResult<AuditTrailListDTO>(
				Array.Empty<AuditTrailListDTO>(),
				null,
				0);
		}

		var normalizedOutcome = NormalizeOutcome(outcome);

		// Cursor over the fixed (OccurredAt DESC, AuditEntryId DESC) ordering. An
		// undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);

		DateTime? afterOccurredAt = DateTime.TryParse(
			fields?[0],
			CultureInfo.InvariantCulture,
			DateTimeStyles.RoundtripKind,
			out var occurredAt)
			? occurredAt
			: null;

		Guid? afterEntryId = Guid.TryParse(fields?[1], out var entryId)
			? entryId
			: null;

		var hasSeek = afterOccurredAt.HasValue && afterEntryId.HasValue;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _auditRepository.GetAuditTrailPageAsync(
			hasSeek ? afterOccurredAt : null,
			hasSeek ? afterEntryId : null,
			pageSize + 1,
			normalizedOutcome,
			action,
			area,
			paginationRequest.SearchTerm,
			paginationRequest.StartDate,
			paginationRequest.EndDate,
			cancellationToken);

		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(
				page[^1].OccurredAt.ToString("O", CultureInfo.InvariantCulture),
				page[^1].AuditEntryId.ToString("D"))
			: null;

		long? totalCount = hasSeek
			? null
			: await _auditRepository.CountAuditTrailAsync(
				normalizedOutcome,
				action,
				area,
				paginationRequest.SearchTerm,
				paginationRequest.StartDate,
				paginationRequest.EndDate,
				cancellationToken);

		return new KeysetPaginatedResult<AuditTrailListDTO>(page, nextCursor, totalCount);
	}

	public async Task<AuditOutcomeCountsDTO> GetOutcomeCountsAsync(
		string? action,
		string? area,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		CancellationToken cancellationToken)
	{
		if (!CanRead())
		{
			return new AuditOutcomeCountsDTO();
		}

		return await _auditRepository.GetOutcomeCountsAsync(
			action,
			area,
			searchTerm,
			startDate,
			endDate,
			cancellationToken);
	}

	public async Task<IReadOnlyList<AtsAuditEntrySummaryDTO>> GetRecentEntriesAsync(
		string? outcome,
		string? action,
		string? area,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		int take,
		CancellationToken cancellationToken)
	{
		// The same gate the paged read uses. This method exists for the AI assistant, which
		// is available to every ATS role - so the check matters more here than anywhere
		// else in this file.
		if (!CanRead())
		{
			return [];
		}

		// Bounded regardless of what the caller asks for: this feeds a chat answer, and a
		// large page would blow out the model's context for no benefit.
		var clampedTake = Math.Clamp(take, 1, MaxAssistantEntries);

		// Null cursor values: the assistant always reads the newest entries and never
		// paginates, so it takes the first page of the existing keyset query.
		var rows = await _auditRepository.GetAuditTrailPageAsync(
			afterOccurredAt: null,
			afterEntryId: null,
			clampedTake,
			NormalizeOutcome(outcome),
			action,
			area,
			searchTerm,
			startDate,
			endDate,
			cancellationToken);

		return rows
			.Select(row => new AtsAuditEntrySummaryDTO
			{
				OccurredAt = row.OccurredAt,
				Action = row.Action,
				Area = row.Area,
				Outcome = row.Outcome,
				UserFullName = row.UserFullName,

				// Truncated because an exception message can run to thousands of characters
				// and is text an attacker can influence; the screen shows it in full.
				FailureReason = Truncate(row.FailureReason, MaxFailureReasonLength)
			})
			.ToArray();
	}

	public async Task<AtsAuditExportDTO> ExportAuditTrailAsync(
		string? outcome,
		string? action,
		string? area,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		CancellationToken cancellationToken)
	{
		// A download is a stronger action than a screen read - the file leaves the system -
		// so this throws rather than returning an empty workbook. A caller who cannot read
		// the trail should be told, not handed a plausible-looking empty file.
		if (!CanRead())
		{
			throw new ForbiddenException("The audit trail is available to platform administrators only.");
		}

		var rows = await _auditRepository.GetAuditTrailPageAsync(
			afterOccurredAt: null,
			afterEntryId: null,
			MaxExportRows,
			NormalizeOutcome(outcome),
			action,
			area,
			searchTerm,
			startDate,
			endDate,
			cancellationToken);

		var content = AtsAuditWorkbookWriter.Write(rows);

		return new AtsAuditExportDTO
		{
			Content = content,
			FileName = BuildExportFileName()
		};
	}

	// Timestamped rather than named from caller input, so a filter value can never reach
	// the Content-Disposition header.
	private static string BuildExportFileName() =>
		$"ats-audit-trail-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";

	// Super admin only, and deliberately not IAtsAccessScopeResolver: this screen is not
	// client-scoped, because a trail the audited user can read is a weaker control. A
	// caller without the right reads an empty list rather than a 403, which is how every
	// other ATS list behaves.
	private bool CanRead()
	{
		if (_currentUser.IsAuthenticated && _currentUser.IsPlatformSuperAdmin)
		{
			return true;
		}

		_logger.LogWarning(
			"Audit trail read denied for user {UserId}: platform super admin is required",
			_currentUser.UserId);

		return false;
	}

	private static string? Truncate(string? value, int maxLength) =>
		value is not null && value.Length > maxLength
			? value[..maxLength]
			: value;

	// An unrecognised outcome would otherwise reach the repository as a literal filter and
	// silently return nothing; treat it as "no filter" instead.
	private static string? NormalizeOutcome(string? outcome) =>
		!string.IsNullOrWhiteSpace(outcome)
			&& AuditOutcome.All.FirstOrDefault(known =>
				string.Equals(known, outcome.Trim(), StringComparison.OrdinalIgnoreCase)) is { } matched
			? matched
			: null;
}
