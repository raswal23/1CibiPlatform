namespace ATS.Features.DownloadIndividualReport;

public record DownloadIndividualReportHandlerRequest(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest) : ICommand<Stream>;

public class DownloadIndividualReportHandler : ICommandHandler<DownloadIndividualReportHandlerRequest, Stream>
{
	private readonly IReportService _reportService;
	public DownloadIndividualReportHandler(IReportService reportService)
	{
		_reportService = reportService;
	}

	public async Task<Stream> Handle(DownloadIndividualReportHandlerRequest request, CancellationToken cancellationToken)
	{
		var stream = await _reportService.DownloadIndividualReportAsync(request.downloadInvididualRequest, cancellationToken);
		return stream;
	}
}
