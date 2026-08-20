namespace ATS.Services.Report;

public interface IReportService
{
	Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO, CancellationToken cancellationToken = default);
	Task<KeysetPaginatedResult<ReportListDTO>> GetReportsAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<ReportResultDTO> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
	Task<Stream> DownloadIndividualReportAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken);
	Task<Stream> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken);
}
