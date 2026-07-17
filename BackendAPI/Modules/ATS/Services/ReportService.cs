namespace ATS.Services;

public class ReportService : IReportService
{
	private readonly ILogger<ReportService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IConfiguration _configuration;
	private readonly IObjectStorageService _objectStorageService;
	private readonly string _folderName;

	public ReportService(
		ILogger<ReportService> logger,
		IATSRepository atsRepository,
		IConfiguration configuration,
		IObjectStorageService objectStorageService)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_configuration = configuration;
		_objectStorageService = objectStorageService;
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
				existingReport.ReportUploadedAt = DateTime.UtcNow;

				return await _atsRepository.UpdateReportDetailsAsync(existingReport, cancellationToken);
			}

			var reportDetails = new ReportDetails
			{
				ReportFileId = Guid.CreateVersion7(),
				EmailInvitationRequestId = reportDetailsDTO.EmailInvitationRequestId,
				HitStatus = reportDetailsDTO.HitStatus,
				ReportStatus = reportDetailsDTO.ReportStatus,
				ReportFileName= reportDetailsDTO.ReportFile.FileName,
				ReportFileKey = fileKey,
				ReportUploadedAt = DateTime.UtcNow
			};

			return await _atsRepository.AddReportDetailsAsync(reportDetails, cancellationToken);
		}
		catch (Exception ex)
		{
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

	public Task<PaginatedResult<ReportListDTO>> GetReportsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetReports",
			Step = "FetchingReports",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching reports with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm)
			? _atsRepository.GetReportsAsync(paginationRequest, cancellationToken)
			: _atsRepository.SearchReportsAsync(paginationRequest, cancellationToken);
	}
}
