namespace ATS.Features.DownloadIndividualReport;

public record DownloadIndividualReportHandlerRequest(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest) : ICommand<DownloadIndividualReportResult>;

public record DownloadIndividualReportResult(Stream zipStream);

public class DownloadIndividualReportHandlerRequestValidator
	: AbstractValidator<DownloadIndividualReportHandlerRequest>
{
	public DownloadIndividualReportHandlerRequestValidator()
	{
		RuleFor(x => x.downloadInvididualRequest)
			.NotNull()
			.WithMessage("Request is required.");

		RuleFor(x => x.downloadInvididualRequest.SubjectName)
			.NotEmpty()
			.WithMessage("Subject name is required.")
			.MaximumLength(200)
			.WithMessage("Subject name must not exceed 200 characters.");

		RuleFor(x => x.downloadInvididualRequest.FileDocuments)
			.NotNull()
			.WithMessage("At least one document is required.")
			.Must(x => x.Any())
			.WithMessage("At least one document is required.");

		RuleForEach(x => x.downloadInvididualRequest.FileDocuments)
			.ChildRules(document =>
			{
				document.RuleFor(x => x.FileKey)
					.NotEmpty()
					.WithMessage("File key is required.");

				document.RuleFor(x => x.FileName)
					.NotEmpty()
					.WithMessage("File name is required.")
					.MaximumLength(255)
					.WithMessage("File name must not exceed 255 characters.");
			});
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
		var stream = await _reportService.DownloadIndividualReportAsync(request.downloadInvididualRequest, cancellationToken);
		return new DownloadIndividualReportResult(stream);
	}
}
