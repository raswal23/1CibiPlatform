namespace ATS.Services;

public interface IReportService
{
	Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO, CancellationToken cancellationToken = default);
   Task<PaginatedResult<ReportListDTO>> GetReportsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
}
