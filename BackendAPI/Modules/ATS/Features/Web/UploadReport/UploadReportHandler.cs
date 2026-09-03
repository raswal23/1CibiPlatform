namespace ATS.Features.Web.UploadReport;

public record UploadReportCommand(ReportDetailsDTO ReportDetailsDTO) : ICommand<UploadReportResult>;

public record UploadReportResult(bool IsUploaded);

public class UploadReportCommandValidator : AbstractValidator<UploadReportCommand>
{
	public UploadReportCommandValidator()
	{
		RuleFor(x => x.ReportDetailsDTO.EmailInvitationRequestId)
			.NotEmpty()
			.WithMessage("Email Invitation Request ID is required.");

		RuleFor(x => x.ReportDetailsDTO.HitStatus)
			.NotEmpty()
			.WithMessage("Hit Status is required.");

		RuleFor(x => x.ReportDetailsDTO.ReportStatus)
			.NotEmpty()
			.WithMessage("Report Status is required.");

		RuleFor(x => x.ReportDetailsDTO.ReportFile)
			.NotNull()
			.WithMessage("Report file is required.")
			.Must(file => file != null && string.Equals(System.IO.Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Only .pdf files are allowed.");
	}
}

public class UploadReportCommandHandler : ICommandHandler<UploadReportCommand, UploadReportResult>
{
	private readonly IReportService _reportService;

	public UploadReportCommandHandler(IReportService reportService)
	{
		_reportService = reportService;
	}

	public async Task<UploadReportResult> Handle(UploadReportCommand request, CancellationToken cancellationToken)
	{
		var isUploaded = await _reportService.UploadReportAsync(request.ReportDetailsDTO, cancellationToken);
		return new UploadReportResult(isUploaded);
	}
}
