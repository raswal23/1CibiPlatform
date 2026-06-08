namespace ATS.Services;

public class DownloadBulkTemplateService : IDownloadBulkTemplateService
{
	private readonly ILogger<DownloadBulkTemplateService> _logger;
	private readonly IObjectStorageService _objectStorageService;
	private readonly string _templateFileName = $"ATS Templates/ATS Bulk Template.csv";


	public DownloadBulkTemplateService(
		ILogger<DownloadBulkTemplateService> logger,
		IConfiguration configuration,
		IObjectStorageService objectStorageService)
	{
		_logger = logger;
		_objectStorageService = objectStorageService;
	}

	public async Task<string> GetBulkTemplateFileUrlAsync()
	{
		// Use Alibaba OSS to generate a presigned URL for the template file stored under templates/
		
		
		var objectKey = _templateFileName;
		var bulkTemplateLink = await _objectStorageService.GenerateDownloadUrlAsync(objectKey, TimeSpan.FromMinutes(15));


		// 15 minute expiry for download links
		return bulkTemplateLink;
			//_objectStorageService.GenerateDownloadUrlAsync(objectKey, TimeSpan.FromMinutes(15));
	}

	public async Task<bool> DownloadBulkTemplateAsync()
	{
		var objectKey = _templateFileName;


		await using var stream = await _objectStorageService.DownloadAsync(objectKey);

		_logger.LogInformation("Successfully retrieved bulk template stream for {ObjectKey}", objectKey);
		return true;
	}

}
