namespace FrontendWebassembly.Services.ATS.Interface;

public interface IReportService
{
    Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO);
}
