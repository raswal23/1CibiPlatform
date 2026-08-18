namespace ATS.Services.Report;

public class ReportService : IReportService
{
	private readonly ILogger<ReportService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IConfiguration _configuration;
	private readonly IObjectStorageService _objectStorageService;
	private readonly string _folderName;
	private readonly IOrderHistoryService _orderHistoryService;
	private readonly IUserClientRepository _userClientRepository;
	private readonly ICurrentUser _currentUser;
	private readonly IUnitOfWork _unitOfWork;

	public ReportService(
		ILogger<ReportService> logger,
		IATSRepository atsRepository,
		IConfiguration configuration,
		IObjectStorageService objectStorageService,
		IOrderHistoryService orderHistoryService,
		IUserClientRepository userClientRepository,
		ICurrentUser currentUser,
		IUnitOfWork unitOfWork)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_configuration = configuration;
		_objectStorageService = objectStorageService;
		_orderHistoryService = orderHistoryService;
		_userClientRepository = userClientRepository;
		_currentUser = currentUser;
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

	public async Task<PaginatedResult<ReportListDTO>> GetReportsAsync(PaginationRequest paginationRequest, string? sortColumn, bool sortDescending, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetReports",
			Step = "FetchingReports",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching reports with pagination: {@Context}", logContext);
		if (!_currentUser.IsAuthenticated
			|| _currentUser.UserId is not { } userId
			|| userId == Guid.Empty)
		{
			return new PaginatedResult<ReportListDTO>(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				0,
				Array.Empty<ReportListDTO>());
		}

		IReadOnlyCollection<int>? clientIds;
		Guid? requiredRequestorId;
		if (_currentUser.IsPlatformSuperAdmin)
		{
			clientIds = null;
			requiredRequestorId = null;
		}
		else if (_currentUser.AtsRoleId is not { } roleId)
		{
			return new PaginatedResult<ReportListDTO>(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				0,
				Array.Empty<ReportListDTO>());
		}
		else if (roleId is AtsRoleIds.PlatformManager or AtsRoleIds.Admin)
		{
			var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
				[userId],
				cancellationToken);
			clientIds = assignments
				.Select(assignment => assignment.ClientId)
				.Distinct()
				.ToArray();
			requiredRequestorId = null;
		}
		else if (roleId is AtsRoleIds.User or AtsRoleIds.Uploader
			&& _currentUser.AtsClientId is { } clientId)
		{
			clientIds = [clientId];
			requiredRequestorId = userId;
		}
		else
		{
			return new PaginatedResult<ReportListDTO>(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				0,
				Array.Empty<ReportListDTO>());
		}

		return await (!string.IsNullOrWhiteSpace(paginationRequest.SearchTerm)
			   || paginationRequest.StartDate.HasValue
			   || paginationRequest.EndDate.HasValue
			? _atsRepository.SearchReportsAsync(
				paginationRequest,
				sortColumn,
				sortDescending,
				clientIds,
				requiredRequestorId,
				cancellationToken)
			: _atsRepository.GetReportsAsync(
				paginationRequest,
				sortColumn,
				sortDescending,
				clientIds,
				requiredRequestorId,
				cancellationToken));
	}

	public async Task<ReportResultDTO> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetReportResult",
			Step = "FetchingReportResult",
			EmailInvitationRequestId = emailInvitationRequestId,
			Timestamp = DateTime.UtcNow
		};

		var result = await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(emailInvitationRequestId, cancellationToken);

		if (string.IsNullOrWhiteSpace(result!.HitStatus))
		{
			result.HitStatus = "-";
		}

		if (result is null)
		{
			_logger.LogError("Failed to upload report {@Context}", logContext);
			throw new NotFoundException($"No report result found for email invitation ID {emailInvitationRequestId}.");
		}

		if (!string.IsNullOrEmpty(result.DiplomaFileKey))
			result.UploadDiplomaAt = result.FilledFormAt;


		if (!string.IsNullOrEmpty(result.BiometricPhotoFileKey))
			result.UploadBiometricPhotoAt = result.FilledFormAt;

		return result;
	}

	public async Task<Stream> DownloadIndividualReportAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "DownloadIndividualReport",
			Step = "GetEachFileAndDownload",
			Pagination = downloadInvididualRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Compiling individual reports for download: {@Context}", logContext);

		var zipStream = new MemoryStream();

		using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
		{
			foreach (var file in downloadInvididualRequest.FileDocuments)
			{
				try
				{
					var entry = archive.CreateEntry(file.FileName ?? "UnknownFile");

					await using var entryStream = entry.Open();
					await using var ossStream = await _objectStorageService.DownloadAsync(file.FileKey!, cancellationToken);

					await ossStream.CopyToAsync(entryStream, cancellationToken);
				}
				catch (Exception ex)
				{
					_logger.LogError("Failed to download individual report {@Context}", logContext);
					throw new InternalServerException($"{ex}");
				}
			}
		}

		zipStream.Position = 0;
		return zipStream;
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

		var zipStream = new MemoryStream();

		try
		{
			var documents = await _atsRepository.GetDownloadDocumentsAsync(downloadMultipleOrderRecordsRequest.EmailInvitaionRequestList, cancellationToken);

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
