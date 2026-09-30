namespace ATS.Features.Web.DownloadApplicationFormPreview;

// A command rather than a query: downloading the applicant's full form is a
// PII export, and AtsAuditBehavior only records commands - the same reason
// DownloadIndividualReport is a command.
public record DownloadApplicationFormPreviewCommand(Guid EmailInvitationRequestId) : ICommand<DownloadApplicationFormPreviewResult>;

public record DownloadApplicationFormPreviewResult(MemoryStream Pdf, string FileName);

public class DownloadApplicationFormPreviewCommandValidator : AbstractValidator<DownloadApplicationFormPreviewCommand>
{
	public DownloadApplicationFormPreviewCommandValidator()
	{
		RuleFor(x => x.EmailInvitationRequestId)
			.NotEmpty()
			.WithMessage("Email invitation request ID is required.");
	}
}

public class DownloadApplicationFormPreviewHandler : ICommandHandler<DownloadApplicationFormPreviewCommand, DownloadApplicationFormPreviewResult>
{
	private readonly IReportService _reportService;
	private readonly IFilePdfService _filePdfService;

	public DownloadApplicationFormPreviewHandler(IReportService reportService, IFilePdfService filePdfService)
	{
		_reportService = reportService;
		_filePdfService = filePdfService;
	}

	public async Task<DownloadApplicationFormPreviewResult> Handle(DownloadApplicationFormPreviewCommand request, CancellationToken cancellationToken)
	{
		// Same scoped read the preview dialog uses, so the PDF can never show
		// more than the caller is allowed to see on screen.
		var preview = await _reportService.GetApplicationFormPreviewAsync(request.EmailInvitationRequestId, cancellationToken);

		var pdf = await _filePdfService.GenerateApplicationFormPreviewPdfAsync(preview, cancellationToken);

		var fileName = string.IsNullOrWhiteSpace(preview.SubjectName)
			? "Application_Form.pdf"
			: $"{preview.SubjectName.Trim().Replace(' ', '_')}_Application_Form.pdf";

		return new DownloadApplicationFormPreviewResult(pdf, fileName);
	}
}
