namespace ATS.Services;

public interface IReportService
{
   Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO, CancellationToken cancellationToken = default);
   Task<PaginatedResult<ReportListDTO>> GetReportsAsync(PaginationRequest paginationRequest, string? sortColumn, bool sortDescending, CancellationToken cancellationToken);
   Task<ReportResultDTO> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
   Task<Stream> DownloadIndividualReport(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken);
	
}
