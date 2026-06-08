namespace ATS.Services;

public interface IDownloadBulkTemplateService
{
	Task<string> GetBulkTemplateFileUrlAsync();

	Task<bool> DownloadBulkTemplateAsync();
}
