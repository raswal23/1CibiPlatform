namespace ATS.Features.Reports.Command.EditSubjectName;

public record EditSubjectNameCommand(EditSubjectNameDTO editSubjectName) : ICommand<EditSubjectNameResult>;

public record EditSubjectNameResult(SubjectNameDTO subject);

public class EditSubjectNameCommandValidator : AbstractValidator<EditSubjectNameCommand>
{
	// Lengths mirror EmailInvitationRequestConfiguration so invalid input fails
	// here with a 400 rather than reaching PostgreSQL as a DbUpdateException.
	public EditSubjectNameCommandValidator()
	{
		RuleFor(x => x.editSubjectName)
			.NotNull().WithMessage("Subject name data is required.");

		When(x => x.editSubjectName != null, () =>
		{
			RuleFor(x => x.editSubjectName.EmailInvitationRequestId)
				.NotEmpty().WithMessage("Email Invitation ID is required.");

			RuleFor(x => x.editSubjectName.FirstName)
				.NotEmpty().WithMessage("First name is required.")
				.MaximumLength(255).WithMessage("First name must not exceed 255 characters.");

			RuleFor(x => x.editSubjectName.LastName)
				.NotEmpty().WithMessage("Last name is required.")
				.MaximumLength(255).WithMessage("Last name must not exceed 255 characters.");

			RuleFor(x => x.editSubjectName.MiddleInitial)
				.MaximumLength(255).WithMessage("Middle name must not exceed 255 characters.")
				.When(x => !string.IsNullOrWhiteSpace(x.editSubjectName.MiddleInitial));
		});
	}
}

public class EditSubjectNameHandler : ICommandHandler<EditSubjectNameCommand, EditSubjectNameResult>
{
	private readonly IReportService _reportService;

	public EditSubjectNameHandler(IReportService reportService)
	{
		_reportService = reportService;
	}

	public async Task<EditSubjectNameResult> Handle(
		EditSubjectNameCommand request,
		CancellationToken cancellationToken)
	{
		var subject = await _reportService.EditSubjectNameAsync(
			request.editSubjectName,
			cancellationToken);

		return new EditSubjectNameResult(subject);
	}
}
