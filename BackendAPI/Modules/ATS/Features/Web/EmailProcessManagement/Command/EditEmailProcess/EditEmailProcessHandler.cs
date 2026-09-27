namespace ATS.Features.Web.EmailProcessManagement.Command.EditEmailProcess;

public record EditEmailProcessCommand(EditEmailProcessDTO editEmailProcess) : ICommand<EditEmailProcessResult>;

public record EditEmailProcessResult(EmailProcessDetailsDTO emailProcess);

public class EditEmailProcessCommandValidator : AbstractValidator<EditEmailProcessCommand>
{
	public EditEmailProcessCommandValidator()
	{
		RuleFor(x => x.editEmailProcess)
			.NotNull().WithMessage("Edit email process data is required.");

		When(x => x.editEmailProcess != null, () =>
		{
			RuleFor(x => x.editEmailProcess.Id)
				.GreaterThan(0).WithMessage("Id is required.");

			// Same two-chain shape as AddEmailProcess, and for the same load-bearing reason: a
			// trailing When() applies to every rule in its chain, so chaining the membership check
			// onto the NotEmpty would switch the NotEmpty off for exactly the empty value it
			// exists to catch.
			RuleFor(x => x.editEmailProcess.EmailProcess)
				.NotEmpty().WithMessage("EmailProcess is required.");

			RuleFor(x => x.editEmailProcess.EmailProcess)
				.Must(process => AtsEmailProcess.All.Contains(process.Trim(), StringComparer.Ordinal))
				.WithMessage($"EmailProcess must be one of: {string.Join(", ", AtsEmailProcess.All)}.")
				.When(x => !string.IsNullOrWhiteSpace(x.editEmailProcess.EmailProcess));

			// Same rules as AddEmailProcess - see the notes there. An edit is the far more
			// common way a bad address gets in, since it is the screen an operator uses every
			// time a team's mailbox changes.
			RuleFor(x => x.editEmailProcess.CCEmail)
				.Must(copyList => EmailCopyList.Validate(copyList) is null)
				.WithMessage(command => EmailCopyList.Validate(command.editEmailProcess.CCEmail));

			RuleFor(x => x.editEmailProcess.IsActive)
				.Equal(false)
				.When(x => EmailCopyList.Split(x.editEmailProcess.CCEmail).Count == 0)
				.WithMessage("A copy list with no email addresses cannot be active.");
		});
	}
}

public class EditEmailProcessHandler : ICommandHandler<EditEmailProcessCommand, EditEmailProcessResult>
{
	private readonly IEmailProcessManagementService _emailProcessManagementService;

	public EditEmailProcessHandler(IEmailProcessManagementService emailProcessManagementService)
	{
		_emailProcessManagementService = emailProcessManagementService;
	}

	public async Task<EditEmailProcessResult> Handle(
		EditEmailProcessCommand request,
		CancellationToken cancellationToken)
	{
		var editedEmailProcess = await _emailProcessManagementService.EditEmailProcessAsync(
			request.editEmailProcess,
			cancellationToken);

		return new EditEmailProcessResult(editedEmailProcess);
	}
}
