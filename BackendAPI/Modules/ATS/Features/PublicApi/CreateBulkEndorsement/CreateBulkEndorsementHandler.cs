namespace ATS.Features.PublicApi.CreateBulkEndorsement;

public record CreateBulkEndorsementCommand(
	IFormFile File,
	string Package,
	string OrderType)
	: ICommand<CreateBulkEndorsementResult>;

public record CreateBulkEndorsementResult(Guid FileId, bool Accepted);

public class CreateBulkEndorsementCommandValidator : AbstractValidator<CreateBulkEndorsementCommand>
{
	// 10 MB. The CSV holds five short columns per subject, so this is far more than a
	// realistic batch needs while still refusing an accidental upload of the wrong file.
	private const long MaxFileSizeBytes = 10 * 1024 * 1024;

	public CreateBulkEndorsementCommandValidator()
	{
		RuleFor(x => x.File)
			.NotNull().WithMessage("A CSV file is required.");

		When(x => x.File is not null, () =>
		{
			RuleFor(x => x.File.Length)
				.GreaterThan(0).WithMessage("The uploaded file is empty.")
				.LessThanOrEqualTo(MaxFileSizeBytes)
				.WithMessage($"The file must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.");

			RuleFor(x => x.File.FileName)
				.Must(fileName => fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
				.WithMessage("The file must be a .csv.");
		});

		RuleFor(x => x.Package)
			.NotEmpty().WithMessage("Package is required.")
			.MaximumLength(100).WithMessage("Package must not exceed 100 characters.");

		RuleFor(x => x.OrderType)
			.NotEmpty().WithMessage("Order type is required.")
			.MaximumLength(20).WithMessage("Order type must not exceed 20 characters.");
	}
}

public class CreateBulkEndorsementHandler
	: ICommandHandler<CreateBulkEndorsementCommand, CreateBulkEndorsementResult>
{
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;

	public CreateBulkEndorsementHandler(IEndorsementSubmissionService endorsementSubmissionService)
	{
		_endorsementSubmissionService = endorsementSubmissionService;
	}

	public async Task<CreateBulkEndorsementResult> Handle(
		CreateBulkEndorsementCommand request,
		CancellationToken cancellationToken)
	{
		var dto = new BulkUploadFileDetailsDTO
		{
			BulkFile = request.File,
			FileName = request.File.FileName,
			PackageType = request.Package,
			OrderType = request.OrderType
		};

		// Same upload path as the console: the file is stored, a Pending row is written,
		// and the Quartz job parses it. Only the source differs.
		var accepted = await _endorsementSubmissionService.InsertBulkSubjectAsync(
			dto,
			cancellationToken,
			OrderHistorySource.PublicApi);

		// Set by the service once the file row exists, so the caller can poll it.
		return new CreateBulkEndorsementResult(dto.FileId, accepted);
	}
}
