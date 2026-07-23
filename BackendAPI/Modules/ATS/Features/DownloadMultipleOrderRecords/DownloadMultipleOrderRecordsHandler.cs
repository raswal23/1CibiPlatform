namespace ATS.Features.DownloadMultipleOrderRecords;

public record DownloadMultipleOrderRecordsHandlerRequest(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest) : ICommand<Stream>;

public class DownloadMultipleOrderRecordsHandler : ICommandHandler<DownloadMultipleOrderRecordsHandlerRequest, Stream>
{
	private readonly IReportService _reportService;

	public DownloadMultipleOrderRecordsHandler(IReportService reportService)
	{
		_reportService = reportService;
	}
	public async Task<Stream> Handle(DownloadMultipleOrderRecordsHandlerRequest request, CancellationToken cancellationToken)
	{
		var stream = await _reportService.DownloadMultipleOrderRecordsAsync(request.downloadMultipleOrderRecordsRequest, cancellationToken);
		return stream;
	}
}
