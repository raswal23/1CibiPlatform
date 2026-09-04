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

	public ReportService(
		ILogger<ReportService> logger,
		IATSRepository atsRepository,
		IConfiguration configuration,
		IObjectStorageService objectStorageService,
		IOrderHistoryService orderHistoryService,
		IAtsAccessScopeResolver accessScopeResolver,
		IUnitOfWork unitOfWork)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_configuration = configuration;
		_objectStorageService = objectStorageService;
		_orderHistoryService = orderHistoryService;
		_accessScopeResolver = accessScopeResolver;
		_unitOfWork = unitOfWork;
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

		// Cursor over the fixed (rank, completedAt?, id) ordering. An undecodable
		// cursor (malformed, stale) means "first page"; rank and id are required —
		// an empty completedAt legitimately round-trips a NULL sort key.
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 3);
		int? afterRank = int.TryParse(fields?[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) ? rank : null;
		Guid? afterId = Guid.TryParse(fields?[2], out var invitationId) ? invitationId : null;
		var hasSeek = afterRank.HasValue && afterId.HasValue;
		DateTime? afterCompletedAt = hasSeek
			&& DateTime.TryParse(fields![1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var completedAt)
			? completedAt : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = isSearch
			? await _atsRepository.SearchReportsPageAsync(
				hasSeek ? afterRank : null, afterCompletedAt, hasSeek ? afterId : null, pageSize + 1,
				paginationRequest.SearchTerm, paginationRequest.StartDate, paginationRequest.EndDate,
				clientIds, requiredRequestorId, cancellationToken)
			: await _atsRepository.GetReportsPageAsync(
				hasSeek ? afterRank : null, afterCompletedAt, hasSeek ? afterId : null, pageSize + 1,
				clientIds, requiredRequestorId, cancellationToken);

		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);
		var nextCursor = hasMore
			? CursorCodec.Encode(
				page[^1].Rank.ToString(CultureInfo.InvariantCulture),
				page[^1].OrderCompletedAt?.ToString("O"),
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
			OrderCompletedAt = x.OrderCompletedAt,
			SelectedPackage = x.SelectPackage,
			Requestor = x.Requestor,
			TicketNumber = x.TicketNumber,
			HitStatus = x.HitStatus
		}).ToList();

		return new KeysetPaginatedResult<ReportListDTO>(items, nextCursor, totalCount);
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
			(AtsDocumentTypes.Diploma, result.DiplomaFileName, result.DiplomaFileKey),
			(AtsDocumentTypes.Coe, result.CoeFileName, result.CoeFileKey),
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

				foreach (var file in files)
				{

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
}
