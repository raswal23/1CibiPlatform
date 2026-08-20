namespace FrontendWebassembly.Services.ATS.Interface;

public interface IReportService
{
    Task<ServiceResponse<bool>> UploadReportAsync(ReportDetailsDTO reportDetailsDTO);
    Task<ServiceResponse<KeysetPaginatedResult<ReportListDTO>>> GetReportsAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null, DateTime? StartDate = null, DateTime? EndDate = null);
    Task<ServiceResponse<ATSResultDetailsDTO>> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId);
	Task<ServiceResponse<HttpResponseMessage>> DownloadDocumentsAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken = default);
	Task<ServiceResponse<HttpResponseMessage>> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken = default);
	Task<ServiceResponse<IReadOnlyList<OrderStatusHistoryDTO>>> GetOrderStatusHistoryAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken = default);
}
