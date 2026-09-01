namespace ATS.Features.PublicApi.DownloadReport;

public record DownloadReportCommand(Guid OrderId, List<string> DocumentTypes)
	: ICommand<DownloadReportResult>;

public record DownloadReportResult(Stream ZipStream, string SubjectName);

public class DownloadReportCommandValidator : AbstractValidator<DownloadReportCommand>
{
	public DownloadReportCommandValidator()
	{
		RuleFor(x => x.OrderId)
			.NotEmpty().WithMessage("Order ID is required.");

		RuleFor(x => x.DocumentTypes)
			.NotEmpty().WithMessage("At least one document type is required.");
	}
}

public class DownloadReportHandler : ICommandHandler<DownloadReportCommand, DownloadReportResult>
{
	private readonly IReportService _reportService;

	public DownloadReportHandler(IReportService reportService)
	{
		_reportService = reportService;
	}

	public async Task<DownloadReportResult> Handle(
		DownloadReportCommand request,
		CancellationToken cancellationToken)
	{
		var dto = new DownloadIndividualDocumentsRequestDTO
		{
			EmailInvitationRequestId = request.OrderId,
			DocumentTypes = request.DocumentTypes
		};

		// The service resolves the caller's scope and rejects an order outside it, so
		// this stays a thin wrapper rather than re-implementing the check.
		var (zipStream, subjectName) = await _reportService.DownloadIndividualReportAsync(
			dto,
			cancellationToken);

		return new DownloadReportResult(zipStream, subjectName);
	}
}
