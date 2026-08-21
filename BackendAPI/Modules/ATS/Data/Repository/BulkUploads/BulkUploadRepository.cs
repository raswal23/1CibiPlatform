namespace ATS.Data.Repository.BulkUploads;

// Deliberately NOT cached, and there is no ATSCacheRepository-style decorator for this
// contract. Status is the most volatile column in the schema - a Pending file becomes
// Processing within one 10-second Quartz tick - so a cached first page would show the
// user exactly the staleness this dashboard exists to remove.
public sealed class BulkUploadRepository : IBulkUploadDashboardRepository
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
				EmailsFailed = group.Count(invitation => invitation.EmailSentStatus == EmailStatus.Error),

				// Pending and Processing are both "still in flight" to a requestor; the
				// job's claim state is an implementation detail of the email worker.
				EmailsPending = group.Count(invitation =>
					invitation.EmailSentStatus == EmailStatus.Pending
					|| invitation.EmailSentStatus == EmailStatus.Processing)
			})
			.ToListAsync(cancellationToken);
	}

	public Task<BulkUploadHeaderDTO?> GetVisibleFileHeaderAsync(
		Guid fileId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken) =>
		ApplyFileScope(
				_dbContext.BulkUploadFileDetails.AsNoTracking(),
				authorizedClientIds,
				requiredUploaderId)
			.Where(file => file.FileID == fileId)
			.Select(file => new BulkUploadHeaderDTO
			{
				FileID = file.FileID,
				FileName = file.FileName,
				Requestor = file.Requestor,
				PackageType = file.PackageType,
				OrderType = file.OrderType,
				Status = file.Status,
				DateCreated = file.DateCreated
			})
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<List<BulkUploadSubjectListDTO>> GetSubjectsPageAsync(
		Guid fileId,
		Guid? afterInvitationId,
		int take,
		string? emailStatus,
		string? searchTerm,
		CancellationToken cancellationToken)
	{
		var pageQuery = BuildSubjectsQuery(fileId, emailStatus, searchTerm);

		if (afterInvitationId.HasValue)
		{
			pageQuery = ApplySubjectSeek(pageQuery, afterInvitationId.Value);
		}

		return await ApplySubjectOrder(pageQuery)
			.Take(take)
			.ToListAsync(cancellationToken);
	}

	public Task<long> CountSubjectsAsync(
		Guid fileId,
		string? emailStatus,
		string? searchTerm,
		CancellationToken cancellationToken) =>
		BuildSubjectsQuery(fileId, emailStatus, searchTerm)
			.LongCountAsync(cancellationToken);

	// The email-status filter is deliberately not applied: like the file-level chips,
	// the subject chips keep showing every bucket's size while one is selected.
	public async Task<BulkUploadSubjectCountsDTO> GetSubjectCountsAsync(
		Guid fileId,
		string? searchTerm,
		CancellationToken cancellationToken)
	{
		var grouped = await BuildSubjectsQuery(
				fileId,
				emailStatus: null,
				searchTerm)
			.GroupBy(subject => subject.EmailSentStatus)
			.Select(group => new StatusCountRow
			{
				Status = group.Key,
				Count = group.LongCount()
			})
			.ToListAsync(cancellationToken);

		return new BulkUploadSubjectCountsDTO
		{
			Pending = CountFor(grouped, EmailStatus.Pending) + CountFor(grouped, EmailStatus.Processing),
			Sent = CountFor(grouped, EmailStatus.Done),
			Failed = CountFor(grouped, EmailStatus.Error),

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

	public Task<List<BulkUploadSubjectListDTO>> GetAllSubjectsForExportAsync(
		Guid fileId,
		CancellationToken cancellationToken) =>
		ApplySubjectOrder(BuildSubjectsQuery(fileId, emailStatus: null, searchTerm: null))
			.ToListAsync(cancellationToken);

	private IQueryable<BulkUploadSubjectListDTO> BuildSubjectsQuery(
		Guid fileId,
		string? emailStatus,
		string? searchTerm)
	{
		var query = _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(invitation => invitation.BulkFileID == fileId);

		// Pending spans two stored values; the other two buckets map one-to-one.
		// Anything outside the vocabulary was already normalized away by the service.
		if (!string.IsNullOrWhiteSpace(emailStatus))
		{
			query = emailStatus switch
			{
				BulkSubjectEmailStatus.Pending => query.Where(invitation =>
					invitation.EmailSentStatus == EmailStatus.Pending
					|| invitation.EmailSentStatus == EmailStatus.Processing),
				BulkSubjectEmailStatus.Sent => query.Where(invitation =>
					invitation.EmailSentStatus == EmailStatus.Done),
				BulkSubjectEmailStatus.Failed => query.Where(invitation =>
					invitation.EmailSentStatus == EmailStatus.Error),
				_ => query
			};
		}

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var search = $"%{searchTerm.Trim()}%";
			query = query.Where(invitation =>
				EF.Functions.ILike(invitation.FirstName ?? string.Empty, search)
				|| EF.Functions.ILike(invitation.LastName ?? string.Empty, search)
				|| EF.Functions.ILike(invitation.EmailAddress ?? string.Empty, search)
				|| EF.Functions.ILike(invitation.MobileNumber ?? string.Empty, search));
		}

		return query.Select(invitation => new BulkUploadSubjectListDTO
		{
			EmailInvitationID = invitation.EmailInvitationID,
			LastName = invitation.LastName,
			FirstName = invitation.FirstName,
			MiddleInitial = invitation.MiddleInitial,
			EmailAddress = invitation.EmailAddress,
			MobileNumber = invitation.MobileNumber,
			EmailSentStatus = invitation.EmailSentStatus,
			EmailSentAt = invitation.EmailSentAt,
			EmailSendAttempts = invitation.EmailSendAttempts,
			EmailClaimedAt = invitation.EmailClaimedAt,
			ApplicationFormStatus = invitation.ApplicationFormStatus,
			FormCompletedAt = invitation.FormCompletedAt,
			OrderStatus = invitation.OrderStatus
		});
	}

	// EmailInvitationID is a v7 GUID minted per CSV row in file order, so ordering by
	// it alone is both unique and the order the user sees in their spreadsheet. That
	// makes the cursor single-field, unlike the file list's (DateCreated, FileID).
	private static IQueryable<BulkUploadSubjectListDTO> ApplySubjectOrder(
		IQueryable<BulkUploadSubjectListDTO> pageQuery) =>
		pageQuery.OrderBy(row => row.EmailInvitationID);

	private static IQueryable<BulkUploadSubjectListDTO> ApplySubjectSeek(
		IQueryable<BulkUploadSubjectListDTO> pageQuery,
		Guid afterInvitationId) =>
		pageQuery.Where(row => row.EmailInvitationID.CompareTo(afterInvitationId) > 0);

	private IQueryable<BulkUploadRowDTO> BuildBulkUploadRowsQuery(
		string? status,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId)
	{
		var query = ApplyFileScope(
			_dbContext.BulkUploadFileDetails.AsNoTracking(),
			authorizedClientIds,
			requiredUploaderId);

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

	// Mirrors ATSRepository.BuildReportRowsQuery: a null client set means unrestricted
	// (super admin), an empty set filters everything out, and UploadedByUserId is the
	// bulk analogue of EmailInvitationRequest.RequestorId. Shared by the list query and
	// the drill-down's visibility check so the two cannot drift apart.
	private static IQueryable<BulkUploadFileDetails> ApplyFileScope(
		IQueryable<BulkUploadFileDetails> query,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId) =>
		query.Where(file => (authorizedClientIds == null
				|| (file.ClientId.HasValue && authorizedClientIds.Contains(file.ClientId.Value)))
			&& (!requiredUploaderId.HasValue
				|| file.UploadedByUserId == requiredUploaderId.Value));

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
