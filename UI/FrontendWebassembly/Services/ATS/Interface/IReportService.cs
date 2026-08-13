namespace FrontendWebassembly.Services.ATS.Interface;

public interface IReportService
{
    Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO);
    Task<PaginatedResult<ReportListDTO>> GetReportsAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, string? SortColumn = null, bool SortDescending = false, DateTime? StartDate = null, DateTime? EndDate = null);
    Task<ATSResultDetailsDTO> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId);
	Task<HttpResponseMessage> DownloadDocumentsAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken = default);
	Task<HttpResponseMessage> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<OrderStatusHistoryDTO>> GetOrderStatusHistoryAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken = default);
}
