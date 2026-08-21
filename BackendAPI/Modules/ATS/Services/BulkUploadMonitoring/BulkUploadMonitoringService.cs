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

	public async Task<BulkUploadSubjectsResultDTO> GetSubjectsAsync(
		Guid fileId,
		KeysetPaginationRequest paginationRequest,
		string? emailStatus,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetBulkUploadSubjects",
			Step = "FetchingSubjects",
			Identity = fileId,
			Pagination = paginationRequest,
			EmailStatus = emailStatus,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching bulk upload subjects: {@Context}", logContext);

		var file = await ResolveVisibleFileAsync(fileId, cancellationToken);

		var normalizedStatus = NormalizeEmailStatus(emailStatus);

		// Single-field cursor: EmailInvitationID is a v7 GUID minted per CSV row, so it
		// is unique, monotonic, and orders the subjects the way the uploaded file does.
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);

		Guid? afterInvitationId = Guid.TryParse(fields?[0], out var invitationId)
			? invitationId
			: null;

		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _bulkUploadRepository.GetSubjectsPageAsync(
			fileId,
			afterInvitationId,
			pageSize + 1,
			normalizedStatus,
			paginationRequest.SearchTerm,
			cancellationToken);

		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(page[^1].EmailInvitationID.ToString("D"))
			: null;

		long? totalCount = afterInvitationId.HasValue
			? null
			: await _bulkUploadRepository.CountSubjectsAsync(
				fileId,
				normalizedStatus,
				paginationRequest.SearchTerm,
				cancellationToken);

		return new BulkUploadSubjectsResultDTO
		{
			File = file,
			Subjects = new KeysetPaginatedResult<BulkUploadSubjectListDTO>(
				page,
				nextCursor,
				totalCount)
		};
	}

	public async Task<BulkUploadSubjectCountsDTO> GetSubjectCountsAsync(
		Guid fileId,
		string? searchTerm,
		CancellationToken cancellationToken)
	{
		await ResolveVisibleFileAsync(fileId, cancellationToken);

		return await _bulkUploadRepository.GetSubjectCountsAsync(
			fileId,
			searchTerm,
			cancellationToken);
	}

	public async Task<BulkUploadSubjectExportDTO> ExportSubjectsAsync(
		Guid fileId,
		CancellationToken cancellationToken)
	{
		var file = await ResolveVisibleFileAsync(fileId, cancellationToken);

		var subjects = await _bulkUploadRepository.GetAllSubjectsForExportAsync(
			fileId,
			cancellationToken);

		var content = new MemoryStream();

		// leaveOpen so disposing the writer does not close the stream the endpoint is
		// about to send.
		await using (var streamWriter = new StreamWriter(
			content,
			Encoding.UTF8,
			leaveOpen: true))
		await using (var csvWriter = new CsvWriter(
			streamWriter,
			CultureInfo.InvariantCulture,
			leaveOpen: true))
		{
			WriteHeader(csvWriter);

			foreach (var subject in subjects)
			{
				WriteRow(csvWriter, subject);
			}

			await csvWriter.FlushAsync();
		}

		content.Position = 0;

		return new BulkUploadSubjectExportDTO
		{
			Content = content,
			FileName = BuildExportFileName(file.FileName)
		};
	}

	private static void WriteHeader(CsvWriter csvWriter)
	{
		csvWriter.WriteField("Last Name");
		csvWriter.WriteField("First Name");
		csvWriter.WriteField("Middle Initial");
		csvWriter.WriteField("Email Address");
		csvWriter.WriteField("Mobile Number");
		csvWriter.WriteField("Email Status");
		csvWriter.WriteField("Email Sent At");
		csvWriter.WriteField("Send Attempts");
		csvWriter.WriteField("Application Form Status");
		csvWriter.WriteField("Form Completed At");
		csvWriter.WriteField("Order Status");

		csvWriter.NextRecord();
	}

	private static void WriteRow(
		CsvWriter csvWriter,
		BulkUploadSubjectListDTO subject)
	{
		csvWriter.WriteField(subject.LastName);
		csvWriter.WriteField(subject.FirstName);
		csvWriter.WriteField(subject.MiddleInitial);
		csvWriter.WriteField(subject.EmailAddress);
		csvWriter.WriteField(subject.MobileNumber);
		csvWriter.WriteField(subject.EmailSentStatus);
		csvWriter.WriteField(FormatTimestamp(subject.EmailSentAt));
		csvWriter.WriteField(subject.EmailSendAttempts);
		csvWriter.WriteField(subject.ApplicationFormStatus);
		csvWriter.WriteField(FormatTimestamp(subject.FormCompletedAt));
		csvWriter.WriteField(subject.OrderStatus);

		csvWriter.NextRecord();
	}

	// Round-trip UTC, so the exported file is unambiguous no matter which timezone
	// opens it. The UI formats for display; the export does not guess.
	private static string FormatTimestamp(DateTime? timestamp) =>
		timestamp?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

	// The stored file name reaches a Content-Disposition header, so strip anything that
	// could break out of the quoted filename or suggest a path.
	private static string BuildExportFileName(string? sourceFileName)
	{
		var baseName = System.IO.Path.GetFileNameWithoutExtension(sourceFileName ?? string.Empty);

		var safeName = new string(baseName
			.Where(character => char.IsLetterOrDigit(character)
				|| character is '-' or '_' or ' ')
			.ToArray())
			.Trim();

		return string.IsNullOrWhiteSpace(safeName)
			? "bulk-upload-subjects.csv"
			: $"{safeName}-subjects.csv";
	}

	// Every drill-down read enforces scope once, here, on the parent file. A caller
	// outside the role ladder and an unknown/foreign file are the same 404 on purpose:
	// the response must not reveal whether a file id exists.
	private async Task<BulkUploadHeaderDTO> ResolveVisibleFileAsync(
		Guid fileId,
		CancellationToken cancellationToken)
	{
		var scope = await _scopeResolver.ResolveAsync(cancellationToken);

		if (scope is not { } accessScope)
		{
			throw new NotFoundException($"Bulk upload file with ID {fileId} not found.");
		}

		var file = await _bulkUploadRepository.GetVisibleFileHeaderAsync(
			fileId,
			accessScope.AuthorizedClientIds,
			accessScope.RequiredOwnerId,
			cancellationToken);

		if (file is null)
		{
			throw new NotFoundException($"Bulk upload file with ID {fileId} not found.");
		}

		return file;
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
					EmailsFailed = rollup?.EmailsFailed ?? 0,
					EmailsPending = rollup?.EmailsPending ?? 0
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

	// Same contract as NormalizeStatus, over the subject-level vocabulary.
	private static string? NormalizeEmailStatus(string? emailStatus)
	{
		if (string.IsNullOrWhiteSpace(emailStatus))
		{
			return null;
		}

		return BulkSubjectEmailStatus.All.FirstOrDefault(known =>
			string.Equals(known, emailStatus.Trim(), StringComparison.OrdinalIgnoreCase));
	}
}
