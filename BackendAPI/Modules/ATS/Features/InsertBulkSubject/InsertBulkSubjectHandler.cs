namespace ATS.Features.InsertBulkSubject;

public record InsertBulkSubjectCommand(BulkUploadFileDetailsDTO file) : ICommand<InsertBulkSubjectResult>;
public record InsertBulkSubjectResult(bool isAdded);

public class InsertBulkSubjectCommandValidator : AbstractValidator<InsertBulkSubjectCommand>
{
	public InsertBulkSubjectCommandValidator()
	{
		RuleFor(x => x.file.FileID)
			.NotEmpty()
			.WithMessage("File ID is required.");

		RuleFor(x => x.file.FileName)
			.NotEmpty()
			.WithMessage("Bulk upload file name is required.");

		RuleFor(x => x.file.Status)
			.NotEmpty()
			.WithMessage("Bulk upload file name is required.");
	}
}
public class InsertBulkSubjectHandler : ICommandHandler<InsertBulkSubjectCommand, InsertBulkSubjectResult>
{
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;
	public InsertBulkSubjectHandler(IEndorsementSubmissionService endorsementSubmissionService)
	{
		_endorsementSubmissionService = endorsementSubmissionService;
	}
	public async Task<InsertBulkSubjectResult> Handle(
		InsertBulkSubjectCommand request, 
		CancellationToken cancellationToken)
	{
		var isAdded = await _endorsementSubmissionService.InsertBulkSubjectAsync(request.file, cancellationToken);
		return new InsertBulkSubjectResult(isAdded);
	}
}
