namespace ATS.Features.DownloadBulkTemplate;

public record DownloadBulkTemplateHandlerRequest() : IQuery<DownloadBulkTemplateResult>;

public record DownloadBulkTemplateResult(string templateLink);

public class DownloadBulkTemplateHandler : IQueryHandler<DownloadBulkTemplateHandlerRequest, DownloadBulkTemplateResult>
{
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;
	public DownloadBulkTemplateHandler(IEndorsementSubmissionService endorsementSubmissionService)
	{
		_endorsementSubmissionService = endorsementSubmissionService;
	}

	public async Task<DownloadBulkTemplateResult> Handle(DownloadBulkTemplateHandlerRequest request, CancellationToken cancellationToken)
	{
		string templateLink = await _endorsementSubmissionService.GetBulkTemplateFileUrlAsync();
		return new DownloadBulkTemplateResult(templateLink);
	}
}
