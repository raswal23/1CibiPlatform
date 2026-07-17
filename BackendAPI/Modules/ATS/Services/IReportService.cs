namespace ATS.Services;

public interface IReportService
{
	Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO, CancellationToken cancellationToken = default);
}
