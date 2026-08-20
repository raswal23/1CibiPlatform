namespace ATS.Features.InsertBulkSubject;

public record InsertBulkSubjectCommand(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO) : ICommand<InsertBulkSubjectResult>;
public record InsertBulkSubjectResult(bool isAdded);

public class InsertBulkSubjectCommandValidator : AbstractValidator<InsertBulkSubjectCommand>
{
	public InsertBulkSubjectCommandValidator()
	{
		RuleFor(x => x.bulkUploadFileDetailsDTO.FileName)
			.NotEmpty()
			.WithMessage("Bulk upload file name is required.");
		RuleFor(x => x.bulkUploadFileDetailsDTO.BulkFile)
			.NotNull()
			.WithMessage("Bulk upload file is required.")
			.Must(file => file != null &&
			 string.Equals(System.IO.Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Only .csv files are allowed.")
			.Must(file => file != null && file.Length <= 25 * 1024 * 1024)
			.WithMessage("File size exceeds the 25 MB limit.");
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
		var isAdded = await _endorsementSubmissionService.InsertBulkSubjectAsync(request.bulkUploadFileDetailsDTO, cancellationToken);
		return new InsertBulkSubjectResult(isAdded);
	}
}
