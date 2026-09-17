namespace ATS.Services.Report;

public class ReportService : IReportService
{
	private readonly ILogger<ReportService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IConfiguration _configuration;
	private readonly IObjectStorageService _objectStorageService;
	private readonly string _folderName;
	private readonly IOrderHistoryService _orderHistoryService;
	private readonly IAtsAccessScopeResolver _accessScopeResolver;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IAtsNotificationService _notificationService;
	private readonly IFilePdfService _filePdfService;

	public ReportService(
		ILogger<ReportService> logger,
		IATSRepository atsRepository,
		IConfiguration configuration,
		IObjectStorageService objectStorageService,
		IOrderHistoryService orderHistoryService,
		IAtsAccessScopeResolver accessScopeResolver,
		IUnitOfWork unitOfWork,
		IAtsNotificationService notificationService,
		IFilePdfService filePdfService)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_configuration = configuration;
		_objectStorageService = objectStorageService;
		_orderHistoryService = orderHistoryService;
		_accessScopeResolver = accessScopeResolver;
		_unitOfWork = unitOfWork;
		_notificationService = notificationService;
		_filePdfService = filePdfService;
		_folderName = _configuration.GetSection("ATS").GetValue<string>("ATSReportFileFolderName", "");
	}

	public async Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO, CancellationToken cancellationToken = default)
	{
		var logContext = new
		{
			Action = "UploadReport",
			Step = "Start",
			EmailInvitationRequestId = reportDetailsDTO.EmailInvitationRequestId,
			ReportStatus = reportDetailsDTO.ReportStatus,
			Timestamp = DateTime.UtcNow
		};

		string orderStatus = OrderStatus.InProgress;
		DateTime? orderCompletedAt = null;

		if (reportDetailsDTO.ReportFile is null)
		{
			throw new BadRequestException("Report file is required.");
		}

		var invitation = await _atsRepository.GetEmailInvitationRequestByIdAsync(reportDetailsDTO.EmailInvitationRequestId, cancellationToken);
		if (invitation.EmailInvitationID == Guid.Empty)
		{
			throw new NotFoundException($"Email invitation with ID {reportDetailsDTO.EmailInvitationRequestId} not found.");
		}

		string fileKey = string.Empty;
		try
		{
			await using var fileStream = reportDetailsDTO.ReportFile.OpenReadStream();
			fileKey = await _objectStorageService.UploadAsync(_folderName, reportDetailsDTO.ReportFile.FileName, fileStream, cancellationToken);

			var existingReport = await _atsRepository.GetReportDetailsByStatusAsync(
				reportDetailsDTO.EmailInvitationRequestId,
				reportDetailsDTO.ReportStatus ?? string.Empty,
				cancellationToken);

			if (reportDetailsDTO.ReportStatus != ReportStatus.InitialReport)
			{
				orderStatus = OrderStatus.Completed;
				orderCompletedAt = DateTime.UtcNow;
			}

			await _unitOfWork.BeginTransactionAsync(cancellationToken);

			await _atsRepository.UpdateOrderStatusAsync(
					reportDetailsDTO.EmailInvitationRequestId,
					orderStatus,
					orderCompletedAt,
					cancellationToken);

			if (existingReport is not null)
			{
				var archiveReport = new ArchiveReport
				{
					ArchiveReportId = Guid.CreateVersion7(),
					EmailInvitationRequestId = existingReport.EmailInvitationRequestId,
					ReportStatus = existingReport.ReportStatus,
					ReportFileName = reportDetailsDTO.ReportFile.FileName,
					ReportFileKey = existingReport.ReportFileKey,
					ReportUploadedAt = existingReport.ReportUploadedAt
				};

				await _atsRepository.AddArchiveReportAsync(archiveReport, cancellationToken);

				existingReport.HitStatus = reportDetailsDTO.HitStatus;
				existingReport.ReportFileKey = fileKey;
				existingReport.ReportFileName = reportDetailsDTO.ReportFile.FileName;
				existingReport.ReportUploadedAt = DateTime.UtcNow;

				var updated = await _atsRepository.UpdateReportDetailsAsync(existingReport, cancellationToken);

				await _orderHistoryService.RecordAsync(
					invitation.EmailInvitationID,
					OrderHistoryEventType.ReportUploaded,
					invitation.OrderStatus,
					OrderStatus.Completed,
					cancellationToken);

				await _unitOfWork.SaveChangesAsync(cancellationToken);

				await _unitOfWork.CommitAsync(cancellationToken);

				// After the commit: the report is the deliverable and it is now durable.
				await _notificationService.RaiseForOrderAsync(
					invitation.EmailInvitationID,
					AtsNotificationType.ReportReady,
					cancellationToken);

				return updated;
			}

			var reportDetails = new ReportDetails
			{
				ReportFileId = Guid.CreateVersion7(),
				EmailInvitationRequestId = reportDetailsDTO.EmailInvitationRequestId,
				HitStatus = reportDetailsDTO.HitStatus,
				ReportStatus = reportDetailsDTO.ReportStatus,
				ReportFileName = reportDetailsDTO.ReportFile.FileName,
				ReportFileKey = fileKey,
				ReportUploadedAt = DateTime.UtcNow
			};

			var added = await _atsRepository.AddReportDetailsAsync(reportDetails, cancellationToken);

			if (added && orderStatus == OrderStatus.Completed && invitation.OrderStatus != OrderStatus.Completed)
				await _orderHistoryService.RecordAsync(
					invitation.EmailInvitationID,
					OrderHistoryEventType.ReportUploaded,
					invitation.OrderStatus,
					OrderStatus.Completed,
					cancellationToken);

			await _unitOfWork.SaveChangesAsync(cancellationToken);

			await _unitOfWork.CommitAsync(cancellationToken);

			if (added)
			{
				// Completed is the terminal state the requestor is waiting for, so it gets
				// the stronger wording; anything else is "a report is ready to read".
				await _notificationService.RaiseForOrderAsync(
					invitation.EmailInvitationID,
					orderStatus == OrderStatus.Completed
						? AtsNotificationType.OrderCompleted
						: AtsNotificationType.ReportReady,
					cancellationToken);
			}

			return added;
		}
		catch (Exception ex)
		{
			await _unitOfWork.RollbackAsync(cancellationToken);

			_logger.LogError(ex, "Failed to upload report {@Context}", logContext);
			if (!string.IsNullOrWhiteSpace(fileKey))
			{
				try
				{
					await _objectStorageService.DeleteAsync(fileKey, cancellationToken);
				}
				catch (Exception deleteEx)
				{
					_logger.LogWarning(deleteEx, "Failed to delete uploaded report file {FileKey}", fileKey);
				}
			}

			throw new InternalServerException($"Failed to upload report. {ex.InnerException?.Message ?? ex.Message}");
		}
	}

	public async Task<KeysetPaginatedResult<ReportListDTO>> GetReportsAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetReports",
			Step = "FetchingReports",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching reports with pagination: {@Context}", logContext);

		// The role ladder lives in AtsAccessScopeResolver now - this used to be an
		// inline copy of it.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			return new KeysetPaginatedResult<ReportListDTO>(Array.Empty<ReportListDTO>(), null, 0);
		}

		var clientIds = scope.AuthorizedClientIds;
		var requiredRequestorId = scope.RequiredOwnerId;

		var isSearch = !string.IsNullOrWhiteSpace(paginationRequest.SearchTerm)
			|| paginationRequest.StartDate.HasValue
			|| paginationRequest.EndDate.HasValue;

		// Cursor over the fixed (createdAt?, id) ordering. The id makes equal
		// creation timestamps deterministic; an empty createdAt preserves legacy
		// rows whose creation timestamp is null.
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);
		Guid? afterId = Guid.TryParse(fields?[1], out var invitationId) ? invitationId : null;
		var hasSeek = afterId.HasValue;
		DateTime? afterCreatedAt = hasSeek
			&& DateTime.TryParse(fields![0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt)
			? createdAt : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = isSearch
			? await _atsRepository.SearchReportsPageAsync(
				afterCreatedAt, hasSeek ? afterId : null, pageSize + 1,
				paginationRequest.SearchTerm, paginationRequest.StartDate, paginationRequest.EndDate,
				clientIds, requiredRequestorId, cancellationToken)
			: await _atsRepository.GetReportsPageAsync(
				afterCreatedAt, hasSeek ? afterId : null, pageSize + 1,
				clientIds, requiredRequestorId, cancellationToken);

		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);
		var nextCursor = hasMore
			? CursorCodec.Encode(
				page[^1].OrderCreatedAt?.ToString("O"),
				page[^1].EmailInvitationID.ToString("D"))
			: null;

		long? totalCount = hasSeek
			? null
			: await (isSearch
				? _atsRepository.CountSearchReportsAsync(
					paginationRequest.SearchTerm,
					paginationRequest.StartDate,
					paginationRequest.EndDate,
					clientIds,
					requiredRequestorId,
					cancellationToken)
				: _atsRepository.CountReportsAsync(
					clientIds,
					requiredRequestorId,
					cancellationToken));

		var items = page.Select(x => new ReportListDTO
		{
			EmailInvitationRequestId = x.EmailInvitationID,
			SubjectName = $"{x.FirstName} {x.LastName}".Trim(),
			FirstName = x.FirstName,
			MiddleInitial = x.MiddleInitial,
			LastName = x.LastName,
			OrderStatus = x.OrderStatus,
			OrderCreatedAt = x.OrderCreatedAt,
			OrderCompletedAt = x.OrderCompletedAt,
			SelectedPackage = x.SelectPackage,
			RushNormal = x.RushNormal,
			Requestor = x.Requestor,
			TicketNumber = x.TicketNumber,
			HitStatus = x.HitStatus,
			FollowUpEmailsRemaining = CalculateFollowUpEmailsRemaining(x)
		}).ToList();

		return new KeysetPaginatedResult<ReportListDTO>(items, nextCursor, totalCount);
	}

	/// <summary>
	/// How many follow-up reminders an order will still receive, as of today.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Computed here rather than in SQL because the answer depends on today's date, and these
	/// rows pass through a cache decorator - a number baked into the projection would be stale
	/// by exactly as long as the entry lives.
	/// </para>
	/// <para>
	/// This MIRRORS the window in ATSRepository.ReleaseDueFollowUpInvitationsAsync and has to
	/// keep mirroring it. Reminders run from day 1 through day N inclusive, measured in Manila
	/// from OrderCreatedAt, so the count left on day D is N - D. If that query's window
	/// changes and this does not, the board will confidently promise reminders that never
	/// arrive - a wrong number here is worse than no column, because an operator will act on
	/// it instead of chasing the candidate themselves.
	/// </para>
	/// <para>
	/// Null means the question does not apply; 0 means the schedule is spent. The caller
	/// renders those differently.
	/// </para>
	/// </remarks>
	private static int? CalculateFollowUpEmailsRemaining(ReportRowDTO row)
	{
		// Data-screening orders have no candidate to email, and 0 is the package's off switch.
		if (!row.ChasesCandidate || row.PackageFollowUpEmail <= 0)
		{
			return null;
		}

		// Nobody who already dealt with the form is chased about it, whatever the schedule
		// says. Same rule as the release query's ApplicationFormStatus clause.
		if (!string.Equals(row.ApplicationFormStatus, ApplicationFormStatus.Pending, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		// A legacy row with no creation timestamp can never satisfy the window, so it is not
		// "0 remaining" - the schedule simply cannot be evaluated for it.
		if (row.OrderCreatedAt is not { } orderCreatedAt)
		{
			return null;
		}

		// Whole days elapsed in Manila, matching the timezone the release query compares in.
		// Comparing the DATES rather than subtracting the instants is what makes this agree
		// with a query whose first reminder fires at the order's own time of day: on the
		// morning of day 1 the instant difference is under 24 hours until that hour arrives,
		// but the reminder is still due today.
		var orderDate = DateOnly.FromDateTime(
			TimeZoneInfo.ConvertTimeFromUtc(
				DateTime.SpecifyKind(orderCreatedAt, DateTimeKind.Utc),
				FollowUpSchedule.TimeZone));

		var today = DateOnly.FromDateTime(
			TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FollowUpSchedule.TimeZone));

		var daysElapsed = today.DayNumber - orderDate.DayNumber;

		// Clamped at both ends: a clock skew that puts the order in the future must not report
		// MORE reminders than the package allows, and an order past its window reports 0.
		var remaining = row.PackageFollowUpEmail - daysElapsed;

		return Math.Clamp(remaining, 0, row.PackageFollowUpEmail);
	}

	public async Task<SubjectNameDTO> EditSubjectNameAsync(EditSubjectNameDTO subjectName, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "EditSubjectName",
			Step = "Start",
			subjectName.EmailInvitationRequestId,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Editing the subject name on an order: {@Context}", logContext);

		// Same scope gate the reports list applies, so a caller can only rename an
		// order they were already allowed to see.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			throw new ForbiddenException("The current user does not have ATS access.");
		}

		var order = await _atsRepository.GetEmailInvitationOwnerAsync(
			subjectName.EmailInvitationRequestId,
			cancellationToken);

		if (order is null)
		{
			_logger.LogError("The order was not found during the subject-name update: {@Context}", logContext);

			throw new NotFoundException($"Email invitation with ID {subjectName.EmailInvitationRequestId} not found.");
		}

		EnsureOrderIsInScope(order, scope);

		var normalized = new EditSubjectNameDTO
		{
			EmailInvitationRequestId = subjectName.EmailInvitationRequestId,
			FirstName = Normalize(subjectName.FirstName),
			MiddleInitial = Normalize(subjectName.MiddleInitial),
			LastName = Normalize(subjectName.LastName)
		};

		var updated = await _atsRepository.UpdateSubjectNameAsync(normalized, cancellationToken);

		if (!updated)
		{
			throw new NotFoundException($"Email invitation with ID {subjectName.EmailInvitationRequestId} not found.");
		}

		_logger.LogInformation("Subject name updated: {@Context}", logContext);

		return new SubjectNameDTO
		{
			EmailInvitationRequestId = normalized.EmailInvitationRequestId,
			FirstName = normalized.FirstName,
			MiddleInitial = normalized.MiddleInitial,
			LastName = normalized.LastName,
			// Matches how the reports list builds SubjectName, so the edited row
			// renders identically to a freshly loaded one.
			SubjectName = $"{normalized.FirstName} {normalized.LastName}".Trim()
		};
	}

	// null AuthorizedClientIds means every client; an empty collection means none.
	// A RequiredOwnerId restricts the caller to orders they personally raised.
	private static void EnsureOrderIsInScope(EmailInvitationOwnerDTO order, AtsAccessScope scope)
	{
		if (scope.AuthorizedClientIds is { } clientIds
			&& (order.ClientId is not { } clientId || !clientIds.Contains(clientId)))
		{
			throw new ForbiddenException("The selected order is outside the current ATS scope.");
		}

		if (scope.RequiredOwnerId is { } ownerId && order.RequestorId != ownerId)
		{
			throw new ForbiddenException("The selected order is outside the current ATS scope.");
		}
	}

	// A blank middle initial is stored as null so the column keeps one
	// representation of "no middle name".
	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	public async Task<ReportResultDTO> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetReportResult",
			Step = "FetchingReportResult",
			EmailInvitationRequestId = emailInvitationRequestId,
			Timestamp = DateTime.UtcNow
		};

		// Any authenticated ATS user could previously read any order's result - subject
		// name, hit status, and every document key - which was also how a caller
		// obtained the keys the download endpoint used to accept.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			throw new NotFoundException($"No report result found for email invitation ID {emailInvitationRequestId}.");
		}

		var result = await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(
			emailInvitationRequestId,
			scope.AuthorizedClientIds,
			scope.RequiredOwnerId,
			cancellationToken);

		// NotFound rather than Forbidden on purpose: a caller must not be able to probe
		// which order ids exist outside their scope.
		if (result is null)
		{
			_logger.LogWarning("No report result in scope for the caller {@Context}", logContext);
			throw new NotFoundException($"No report result found for email invitation ID {emailInvitationRequestId}.");
		}

		if (string.IsNullOrWhiteSpace(result.HitStatus))
		{
			result.HitStatus = "-";
		}

		if (!string.IsNullOrEmpty(result.DiplomaFileKey))
			result.UploadDiplomaAt = result.FilledFormAt;


		if (!string.IsNullOrEmpty(result.BiometricPhotoFileKey))
			result.UploadBiometricPhotoAt = result.FilledFormAt;

		return result;
	}

	public async Task<(Stream ZipStream, string SubjectName)> DownloadIndividualReportAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "DownloadIndividualReport",
			Step = "GetEachFileAndDownload",
			EmailInvitationRequestId = downloadInvididualRequest.EmailInvitationRequestId,
			DocumentTypes = downloadInvididualRequest.DocumentTypes,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Compiling individual reports for download: {@Context}", logContext);

		// This endpoint used to accept object storage keys straight from the caller and
		// hand them to the bucket, which made it a general-purpose read primitive for
		// any authenticated user. Keys are now resolved here, under the caller's scope.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			throw new NotFoundException($"No documents found for email invitation ID {downloadInvididualRequest.EmailInvitationRequestId}.");
		}

		var result = await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(
			downloadInvididualRequest.EmailInvitationRequestId,
			scope.AuthorizedClientIds,
			scope.RequiredOwnerId,
			cancellationToken);

		if (result is null)
		{
			_logger.LogWarning("No documents in scope for the caller {@Context}", logContext);
			throw new NotFoundException($"No documents found for email invitation ID {downloadInvididualRequest.EmailInvitationRequestId}.");
		}

		var requested = new HashSet<string>(
			downloadInvididualRequest.DocumentTypes ?? [],
			StringComparer.OrdinalIgnoreCase);

		// Only the types the caller asked for, and only those the order actually has.
		var files = ResolveRequestedDocuments(result, requested).ToList();

		if (files.Count == 0)
		{
			throw new NotFoundException("None of the requested documents are available for this order.");
		}

		var zipStream = new MemoryStream();

		using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
		{
			foreach (var (fileName, fileKey) in files)
			{
				try
				{
					var entry = archive.CreateEntry(fileName);

					await using var entryStream = entry.Open();
					await using var ossStream = await _objectStorageService.DownloadAsync(fileKey, cancellationToken);

					await ossStream.CopyToAsync(entryStream, cancellationToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to download individual report {@Context}", logContext);
					throw new InternalServerException($"{ex}");
				}
			}
		}

		zipStream.Position = 0;

		var subjectName = string.IsNullOrWhiteSpace(result.SubjectName)
			? "ATS_Documents"
			: result.SubjectName;

		return (zipStream, subjectName);
	}

	/// <summary>
	/// Maps the requested document type names onto the (file name, file key) pairs the
	/// order actually carries. Types with no stored document are skipped.
	/// </summary>
	private static IEnumerable<(string FileName, string FileKey)> ResolveRequestedDocuments(
		ReportResultDTO result,
		IReadOnlySet<string> requested)
	{
		var candidates = new (string Type, string? FileName, string? FileKey)[]
		{
			(AtsDocumentTypes.BiometricPhoto, result.BiometricPhotoFileName, result.BiometricPhotoFileKey),
			(AtsDocumentTypes.Resume, result.ResumeFileName, result.ResumeFileKey),
			(AtsDocumentTypes.GovernmentId, result.IdUploadedFileName, result.IdUploadedFileKey),
			(AtsDocumentTypes.NbiClearance, result.NbiClearanceFileName, result.NbiClearanceFileKey),
			(AtsDocumentTypes.Diploma, result.DiplomaFileName, result.DiplomaFileKey),
			(AtsDocumentTypes.Coe, result.CoeFileName, result.CoeFileKey),
			(AtsDocumentTypes.Coe1, result.Coe1FileName, result.Coe1FileKey),
			(AtsDocumentTypes.Coe2, result.Coe2FileName, result.Coe2FileKey),
			(AtsDocumentTypes.Coe3, result.Coe3FileName, result.Coe3FileKey),
			(AtsDocumentTypes.ConsentForm, result.ConsentFormFileName, result.ConsentFormFileKey),
			(AtsDocumentTypes.Report, result.UploadedReportFileName, result.UploadedReportFileKey),
		};

		foreach (var (type, fileName, fileKey) in candidates)
		{
			if (requested.Contains(type)
				&& !string.IsNullOrWhiteSpace(fileName)
				&& !string.IsNullOrWhiteSpace(fileKey))
			{
				yield return (fileName, fileKey);
			}
		}
	}

	public async Task<Stream> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken)
	{

		var logContext = new
		{
			Action = "DownloadMultipleOrderRecords",
			Step = "GetEachFileAndDownload",
			Pagination = downloadMultipleOrderRecordsRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Compiling multiple order records for download: {@Context}", logContext);

		// Same finding as DownloadIndividualReportAsync: the id list came from the
		// caller and was never checked against their scope.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			throw new NotFoundException("No order records found.");
		}

		var zipStream = new MemoryStream();

		try
		{
			var documents = await _atsRepository.GetDownloadDocumentsAsync(
				downloadMultipleOrderRecordsRequest.EmailInvitaionRequestList,
				scope.AuthorizedClientIds,
				scope.RequiredOwnerId,
				cancellationToken);

			using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);

			foreach (var applicant in documents.GroupBy(x => x.EmailInvitationRequestId))
			{
				var files = applicant
						 .Where(x => !string.IsNullOrWhiteSpace(x.FileKey))
						 .ToList();

				if (files.Count == 0)
					continue;

				using var output = new PdfDocument();

				// The compiled record opens with the generated application form
				// (the same QuestPDF render as the preview download), placed just
				// before the consent form the applicant signed. Rendered fresh here
				// rather than stored, so it always reflects the current answers.
				var consentFormIndex = files.FindIndex(file =>
					string.Equals(file.DocumentType, AtsDocumentTypes.ConsentForm, StringComparison.OrdinalIgnoreCase));
				var formInsertIndex = consentFormIndex >= 0 ? consentFormIndex : files.Count;
				var appended = false;

				async Task AppendApplicationFormAsync()
				{
					var preview = await _atsRepository.GetApplicationFormPreviewAsync(
						applicant.Key, scope.AuthorizedClientIds, scope.RequiredOwnerId, cancellationToken);

					if (preview is null)
						return;

					await using var formPdf = await _filePdfService.GenerateApplicationFormPreviewPdfAsync(preview, cancellationToken);
					using var formInput = PdfReader.Open(formPdf, PdfDocumentOpenMode.Import);

					foreach (var page in formInput.Pages)
					{
						output.AddPage(page);
					}
				}

				for (var index = 0; index < files.Count; index++)
				{
					if (index == formInsertIndex)
					{
						await AppendApplicationFormAsync();
						appended = true;
					}

					var file = files[index];

					await using var ossStream = await _objectStorageService.DownloadAsync(file.FileKey, cancellationToken);

					using var memoryStream = new MemoryStream();

					await ossStream.CopyToAsync(memoryStream, cancellationToken);

					memoryStream.Position = 0;

					using var input = PdfReader.Open(memoryStream, PdfDocumentOpenMode.Import);

					foreach (var page in input.Pages)
					{
						output.AddPage(page);
					}
				}

				if (!appended)
				{
					await AppendApplicationFormAsync();
				}

				using var mergedPdf = new MemoryStream();

				output.Save(mergedPdf);

				mergedPdf.Position = 0;

				var entry = archive.CreateEntry($"{applicant.First().SubjectName.Replace(" ", "_")}.pdf");

				await using var entryStream = entry.Open();

				mergedPdf.Position = 0;

				await mergedPdf.CopyToAsync(entryStream, cancellationToken);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError("Failed to download multiple order records {@Context}", logContext);
			throw new InternalServerException($"{ex}");
		}

		zipStream.Position = 0;
		return zipStream;
	}

	public async Task<ApplicationFormPreviewDTO> GetApplicationFormPreviewAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		// NotFound rather than Forbidden, matching the report result lookup: a caller
		// must not be able to probe which order ids exist outside their scope.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			throw new NotFoundException($"No application form found for email invitation ID {emailInvitationRequestId}.");
		}

		var preview = await _atsRepository.GetApplicationFormPreviewAsync(
			emailInvitationRequestId,
			scope.AuthorizedClientIds,
			scope.RequiredOwnerId,
			cancellationToken);

		if (preview is null)
		{
			throw new NotFoundException($"No application form found for email invitation ID {emailInvitationRequestId}.");
		}

		return preview;
	}
}
