namespace ATS.Services.BulkUploadMonitoring;

public sealed class BulkUploadMonitoringService : IBulkUploadMonitoringService
{
	private readonly ILogger<BulkUploadMonitoringService> _logger;
	private readonly IBulkUploadRepository _bulkUploadRepository;
	private readonly IAtsAccessScopeResolver _scopeResolver;

	public BulkUploadMonitoringService(
		ILogger<BulkUploadMonitoringService> logger,
		IBulkUploadRepository bulkUploadRepository,
		IAtsAccessScopeResolver scopeResolver)
	{
		_logger = logger;
		_bulkUploadRepository = bulkUploadRepository;
		_scopeResolver = scopeResolver;
	}

	public async Task<KeysetPaginatedResult<BulkUploadListDTO>> GetBulkUploadsAsync(
		KeysetPaginationRequest paginationRequest,
		string? status,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetBulkUploads",
			Step = "FetchingBulkUploads",
			Pagination = paginationRequest,
			Status = status,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching bulk uploads with pagination: {@Context}", logContext);

		var scope = await _scopeResolver.ResolveAsync(cancellationToken);

		// A caller outside the role ladder reads an empty list rather than a 403, which
		// is how every other ATS list behaves.
		if (scope is not { } accessScope)
		{
			return new KeysetPaginatedResult<BulkUploadListDTO>(
				Array.Empty<BulkUploadListDTO>(),
				null,
				0);
		}

		var normalizedStatus = NormalizeStatus(status);

		// Cursor over the fixed (DateCreated DESC, FileID ASC) ordering. An undecodable
		// cursor (malformed, stale) means "first page"; both fields are required because
		// DateCreated is a non-nullable column.
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);

		DateTime? afterDateCreated = DateTime.TryParse(
			fields?[0],
			CultureInfo.InvariantCulture,
			DateTimeStyles.RoundtripKind,
			out var dateCreated)
			? dateCreated
			: null;

		Guid? afterFileId = Guid.TryParse(fields?[1], out var fileId)
			? fileId
			: null;

		var hasSeek = afterDateCreated.HasValue && afterFileId.HasValue;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _bulkUploadRepository.GetBulkUploadsPageAsync(
			hasSeek ? afterDateCreated : null,
			hasSeek ? afterFileId : null,
			pageSize + 1,
			normalizedStatus,
			paginationRequest.SearchTerm,
			paginationRequest.StartDate,
			paginationRequest.EndDate,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(
				page[^1].DateCreated.ToString("O", CultureInfo.InvariantCulture),
				page[^1].FileID.ToString("D"))
			: null;

		long? totalCount = hasSeek
			? null
			: await _bulkUploadRepository.CountBulkUploadsAsync(
				normalizedStatus,
				paginationRequest.SearchTerm,
				paginationRequest.StartDate,
				paginationRequest.EndDate,
				accessScope.AuthorizedClientIds,
				accessScope.RequiredOwnerId,
				cancellationToken);

		var items = await ProjectWithRollupAsync(page, cancellationToken);

		return new KeysetPaginatedResult<BulkUploadListDTO>(items, nextCursor, totalCount);
	}

	public async Task<BulkUploadStatusCountsDTO> GetStatusCountsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		CancellationToken cancellationToken)
	{
		var scope = await _scopeResolver.ResolveAsync(cancellationToken);

		if (scope is not { } accessScope)
		{
			return new BulkUploadStatusCountsDTO();
		}

		return await _bulkUploadRepository.GetStatusCountsAsync(
			searchTerm,
			startDate,
			endDate,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);
	}

	// One rollup query for the whole page, then a left join in memory. A file the bulk
	// job has not parsed yet has no invitations at all and legitimately gets zeros.
	private async Task<List<BulkUploadListDTO>> ProjectWithRollupAsync(
		List<BulkUploadRowDTO> page,
		CancellationToken cancellationToken)
	{
		if (page.Count == 0)
		{
			return [];
		}

		var fileIds = page
			.Select(row => row.FileID)
			.ToArray();

		var rollups = await _bulkUploadRepository.GetInvitationRollupAsync(
			fileIds,
			cancellationToken);

		var rollupByFileId = rollups.ToDictionary(rollup => rollup.FileID);

		return page
			.Select(row =>
			{
				rollupByFileId.TryGetValue(row.FileID, out var rollup);

				return new BulkUploadListDTO
				{
					FileID = row.FileID,
					FileName = row.FileName,
					Requestor = row.Requestor,
					PackageType = row.PackageType,
					OrderType = row.OrderType,
					Status = row.Status,
					DateCreated = row.DateCreated,
					ClaimedAt = row.ClaimedAt,
					SubjectCount = rollup?.SubjectCount ?? 0,
					EmailsSent = rollup?.EmailsSent ?? 0,
					EmailsFailed = rollup?.EmailsFailed ?? 0
				};
			})
			.ToList();
	}

	// Blank or unrecognised means "All". The validator rejects an unknown status before
	// the handler runs; this keeps the service safe when called directly.
	private static string? NormalizeStatus(string? status)
	{
		if (string.IsNullOrWhiteSpace(status))
		{
			return null;
		}

		return BulkFileStatus.All.FirstOrDefault(known =>
			string.Equals(known, status.Trim(), StringComparison.OrdinalIgnoreCase));
	}
}
