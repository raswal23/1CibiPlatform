namespace ATS.Features.DownloadIndividualReport;

public record DownloadIndividualReportHandlerRequest(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest) : ICommand<DownloadIndividualReportResult>;

public record DownloadIndividualReportResult(Stream zipStream, string SubjectName);

public class DownloadIndividualReportHandlerRequestValidator
	: AbstractValidator<DownloadIndividualReportHandlerRequest>
{
	public DownloadIndividualReportHandlerRequestValidator()
	{
		RuleFor(x => x.downloadInvididualRequest)
			.NotNull()
			.WithMessage("Request is required.");

		RuleFor(x => x.downloadInvididualRequest.EmailInvitationRequestId)
			.NotEmpty()
			.WithMessage("Email invitation request id is required.");

		RuleFor(x => x.downloadInvididualRequest.DocumentTypes)
			.NotNull()
			.WithMessage("At least one document is required.")
			.Must(x => x.Any())
			.WithMessage("At least one document is required.");

		// Reject unknown type names outright rather than silently returning a short
		// zip - a typo in the UI should be loud.
		RuleForEach(x => x.downloadInvididualRequest.DocumentTypes)
			.Must(AtsDocumentTypes.All.Contains)
			.WithMessage("Unknown document type.");
	}
}

public class DownloadIndividualReportHandler : ICommandHandler<DownloadIndividualReportHandlerRequest, DownloadIndividualReportResult>
{
	private readonly IReportService _reportService;
	public DownloadIndividualReportHandler(IReportService reportService)
	{
		_reportService = reportService;
	}

	public async Task<DownloadIndividualReportResult> Handle(DownloadIndividualReportHandlerRequest request, CancellationToken cancellationToken)
	{
		var (zipStream, subjectName) = await _reportService.DownloadIndividualReportAsync(request.downloadInvididualRequest, cancellationToken);
		return new DownloadIndividualReportResult(zipStream, subjectName);
	}
}
