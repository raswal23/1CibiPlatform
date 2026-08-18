namespace FrontendWebassembly.Services.ATS.Report;

public interface IReportService
{
    Task<ServiceResponse<bool>> UploadReportAsync(ReportDetailsDTO reportDetailsDTO);
    Task<ServiceResponse<PaginatedResult<ReportListDTO>>> GetReportsAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, string? SortColumn = null, bool SortDescending = false, DateTime? StartDate = null, DateTime? EndDate = null);
    Task<ServiceResponse<ATSResultDetailsDTO>> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId);
	Task<ServiceResponse<HttpResponseMessage>> DownloadDocumentsAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken = default);
	Task<ServiceResponse<HttpResponseMessage>> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken = default);
	Task<ServiceResponse<IReadOnlyList<OrderStatusHistoryDTO>>> GetOrderStatusHistoryAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken = default);
}
