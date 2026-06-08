namespace ATS.Features.DownloadBulkTemplate;

public record DownloadBulkTemplateCommand() : ICommand<DownloadBulkTemplateResult>;

public record DownloadBulkTemplateResult(string templateLink);

public class DownloadBulkTemplateHandler : ICommandHandler<DownloadBulkTemplateCommand, DownloadBulkTemplateResult>
{
	private readonly IDownloadBulkTemplateService _downloadBulkTemplateService;
	public DownloadBulkTemplateHandler(IDownloadBulkTemplateService downloadBulkTemplateService)
	{
		_downloadBulkTemplateService = downloadBulkTemplateService;
	}

	public async Task<DownloadBulkTemplateResult> Handle(DownloadBulkTemplateCommand request, CancellationToken cancellationToken)
	{
		// Call the service to ensure the template is retrievable
		string templateLink = await _downloadBulkTemplateService.GetBulkTemplateFileUrlAsync();
		return new DownloadBulkTemplateResult(templateLink);
	}
}
