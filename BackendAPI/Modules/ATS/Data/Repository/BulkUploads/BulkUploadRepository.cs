namespace ATS.Data.Repository.BulkUploads;

// Deliberately NOT cached, and there is no ATSCacheRepository-style decorator for this
// contract. Status is the most volatile column in the schema - a Pending file becomes
// Processing within one 10-second Quartz tick - so a cached first page would show the
// user exactly the staleness this dashboard exists to remove.
public sealed class BulkUploadRepository : IBulkUploadRepository
{
	private readonly ATSDBContext _dbContext;

	public BulkUploadRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public async Task<List<BulkUploadRowDTO>> GetBulkUploadsPageAsync(
		DateTime? afterDateCreated,
		Guid? afterFileId,
		int take,
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken)
	{
		var pageQuery = BuildBulkUploadRowsQuery(
			status,
			searchTerm,
			startDate,
			endDate,
			authorizedClientIds,
			requiredUploaderId);

		if (afterDateCreated.HasValue && afterFileId.HasValue)
		{
			pageQuery = ApplySeek(pageQuery, afterDateCreated.Value, afterFileId.Value);
		}

		return await ApplyOrder(pageQuery)
			.Take(take)
			.ToListAsync(cancellationToken);
	}

	public Task<long> CountBulkUploadsAsync(
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken) =>
		BuildBulkUploadRowsQuery(
				status,
				searchTerm,
				startDate,
				endDate,
				authorizedClientIds,
				requiredUploaderId)
			.LongCountAsync(cancellationToken);

	// One round-trip for every bucket. The status filter is deliberately not applied:
	// the dashboard chips must keep showing every bucket's size while one is selected.
	public async Task<BulkUploadStatusCountsDTO> GetStatusCountsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken)
	{
		var grouped = await BuildBulkUploadRowsQuery(
				status: null,
				searchTerm,
				startDate,
				endDate,
				authorizedClientIds,
				requiredUploaderId)
			.GroupBy(row => row.Status)
			.Select(group => new StatusCountRow
			{
				Status = group.Key,
				Count = group.LongCount()
			})
			.ToListAsync(cancellationToken);

		return new BulkUploadStatusCountsDTO
		{
			Pending = CountFor(grouped, BulkFileStatus.Pending),
			Processing = CountFor(grouped, BulkFileStatus.Processing),
			Done = CountFor(grouped, BulkFileStatus.Done),

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

	// Fetched once per page rather than per row. Uses IX_EmailInvitationRequest_BulkFileID.
	public async Task<List<BulkFileInvitationRollupDTO>> GetInvitationRollupAsync(
		IReadOnlyCollection<Guid> fileIds,
		CancellationToken cancellationToken)
	{
		if (fileIds.Count == 0)
		{
			return [];
		}

		return await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(invitation => invitation.BulkFileID.HasValue
				&& fileIds.Contains(invitation.BulkFileID.Value))
			.GroupBy(invitation => invitation.BulkFileID!.Value)
			.Select(group => new BulkFileInvitationRollupDTO
			{
				FileID = group.Key,
				SubjectCount = group.Count(),
				EmailsSent = group.Count(invitation => invitation.EmailSentStatus == EmailStatus.Done),
				EmailsFailed = group.Count(invitation => invitation.EmailSentStatus == EmailStatus.Error)
			})
			.ToListAsync(cancellationToken);
	}

	private IQueryable<BulkUploadRowDTO> BuildBulkUploadRowsQuery(
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId)
	{
		// Mirrors ATSRepository.BuildReportRowsQuery: a null client set means
		// unrestricted (super admin), an empty set filters everything out, and
		// UploadedByUserId is the bulk analogue of EmailInvitationRequest.RequestorId.
		var query = _dbContext.BulkUploadFileDetails
			.AsNoTracking()
			.Where(file => (authorizedClientIds == null
					|| (file.ClientId.HasValue && authorizedClientIds.Contains(file.ClientId.Value)))
				&& (!requiredUploaderId.HasValue
					|| file.UploadedByUserId == requiredUploaderId.Value));

		if (!string.IsNullOrWhiteSpace(status))
		{
			query = query.Where(file => file.Status == status);
		}

		if (startDate.HasValue)
		{
			var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
			query = query.Where(file => file.DateCreated >= start);
		}

		if (endDate.HasValue)
		{
			var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
			query = query.Where(file => file.DateCreated < end);
		}

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var search = $"%{searchTerm.Trim()}%";
			query = query.Where(file =>
				EF.Functions.ILike(file.FileName ?? string.Empty, search)
				|| EF.Functions.ILike(file.Requestor ?? string.Empty, search)
				|| EF.Functions.ILike(file.PackageType ?? string.Empty, search)
				|| EF.Functions.ILike(file.OrderType ?? string.Empty, search)
				|| EF.Functions.ILike(file.Status ?? string.Empty, search));
		}

		return query.Select(file => new BulkUploadRowDTO
		{
			FileID = file.FileID,
			FileName = file.FileName,
			Requestor = file.Requestor,
			PackageType = file.PackageType,
			OrderType = file.OrderType,
			Status = file.Status,
			DateCreated = file.DateCreated,
			ClaimedAt = file.ClaimedAt
		});
	}

	// The single bulk upload ordering: newest upload first, unique FileID as the
	// tiebreaker. ApplySeek below must mirror this expression exactly.
	private static IQueryable<BulkUploadRowDTO> ApplyOrder(IQueryable<BulkUploadRowDTO> pageQuery) =>
		pageQuery
			.OrderByDescending(row => row.DateCreated)
			.ThenBy(row => row.FileID);

	// Seek predicate for the fixed (DateCreated DESC, FileID ASC) ordering. DateCreated
	// is a required column, so unlike the reports seek there is no NULL branch.
	private static IQueryable<BulkUploadRowDTO> ApplySeek(
		IQueryable<BulkUploadRowDTO> pageQuery,
		DateTime afterDateCreated,
		Guid afterFileId) =>
		pageQuery.Where(row => row.DateCreated < afterDateCreated
			|| (row.DateCreated == afterDateCreated && row.FileID.CompareTo(afterFileId) > 0));
}
