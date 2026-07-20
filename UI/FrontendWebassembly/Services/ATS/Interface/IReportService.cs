namespace FrontendWebassembly.Services.ATS.Interface;

public interface IReportService
{
    Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO);
    Task<PaginatedResult<ReportListDTO>> GetReportsAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, string? SortColumn = null, bool SortDescending = false);
}
