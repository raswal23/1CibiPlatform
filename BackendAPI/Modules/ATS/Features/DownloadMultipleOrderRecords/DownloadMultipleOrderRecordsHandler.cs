namespace ATS.Features.DownloadMultipleOrderRecords;

public record DownloadMultipleOrderRecordsHandlerRequest(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest) : ICommand<DownloadMultipleOrderRecordsResult>;

public record DownloadMultipleOrderRecordsResult(Stream zipStream);

public class DownloadMultipleOrderRecordsHandlerRequestValidator
	: AbstractValidator<DownloadMultipleOrderRecordsHandlerRequest>
{
	public DownloadMultipleOrderRecordsHandlerRequestValidator()
	{
		RuleFor(x => x.downloadMultipleOrderRecordsRequest)
			.NotNull()
			.WithMessage("Request is required.");

		RuleFor(x => x.downloadMultipleOrderRecordsRequest.EmailInvitaionRequestList)
			.NotNull()
			.WithMessage("At least one order record must be selected.")
			.Must(x => x.Any())
			.WithMessage("At least one order record must be selected.");

		RuleForEach(x => x.downloadMultipleOrderRecordsRequest.EmailInvitaionRequestList)
			.NotEmpty()
			.WithMessage("Email Invitation Request ID is required.");
	}
}

public class DownloadMultipleOrderRecordsHandler : ICommandHandler<DownloadMultipleOrderRecordsHandlerRequest, DownloadMultipleOrderRecordsResult>
{
	private readonly IReportService _reportService;

	public DownloadMultipleOrderRecordsHandler(IReportService reportService)
	{
		_reportService = reportService;
	}
	public async Task<DownloadMultipleOrderRecordsResult> Handle(DownloadMultipleOrderRecordsHandlerRequest request, CancellationToken cancellationToken)
	{
		var stream = await _reportService.DownloadMultipleOrderRecordsAsync(request.downloadMultipleOrderRecordsRequest, cancellationToken);
		return new DownloadMultipleOrderRecordsResult(stream);
	}
}
