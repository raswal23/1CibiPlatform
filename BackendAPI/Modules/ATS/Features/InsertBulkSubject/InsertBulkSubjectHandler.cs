namespace ATS.Features.InsertBulkSubject;

public record InsertBulkSubjectCommand(BulkUploadFileDetailsDTO file) : ICommand<InsertBulkSubjectResult>;
public record InsertBulkSubjectResult(Guid FiledID);

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
	private readonly IInsertBulkSubjectService _insertBulkSubjectService;
	public InsertBulkSubjectHandler(IInsertBulkSubjectService insertBulkSubjectService)
	{
		_insertBulkSubjectService = insertBulkSubjectService;
	}
	public async Task<InsertBulkSubjectResult> Handle(
		InsertBulkSubjectCommand request, 
		CancellationToken cancellationToken)
	{
		var id = await _insertBulkSubjectService.InsertBulkSubjectAsync(request.file, cancellationToken);
		return new InsertBulkSubjectResult(id);
	}
}
