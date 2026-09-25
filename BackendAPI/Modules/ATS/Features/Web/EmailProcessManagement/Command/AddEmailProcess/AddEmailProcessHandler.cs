namespace ATS.Features.Web.EmailProcessManagement.Command.AddEmailProcess;

public record AddEmailProcessCommand(AddEmailProcessDTO emailProcess) : ICommand<AddEmailProcessResult>;

public record AddEmailProcessResult(EmailProcessDetailsDTO emailProcess);

public class AddEmailProcessCommandValidator : AbstractValidator<AddEmailProcessCommand>
{
	public AddEmailProcessCommandValidator()
	{
		RuleFor(x => x.emailProcess)
			.NotNull().WithMessage("Email process data is required.");

		When(x => x.emailProcess != null, () =>
		{
			RuleFor(x => x.emailProcess.EmailProcess)
				.NotEmpty().WithMessage("EmailProcess is required.");

			// Closed to the constant rather than length-checked, because the send path looks a
			// process up by this exact string: a row registered against a name nothing sends
			// would be an invisible no-op, and the screen would show it as configured. The
			// message lists the values so a caller does not have to read the source to find
			// out what it may send.
			//
			// A SECOND RuleFor, not another link on the chain above. A trailing When() applies
			// to every rule in its chain, not only the one it follows - chaining these would
			// have switched the NotEmpty off for exactly the empty value it exists to catch.
			RuleFor(x => x.emailProcess.EmailProcess)
				.Must(process => AtsEmailProcess.All.Contains(process.Trim(), StringComparer.Ordinal))
				.WithMessage($"EmailProcess must be one of: {string.Join(", ", AtsEmailProcess.All)}.")
				.When(x => !string.IsNullOrWhiteSpace(x.emailProcess.EmailProcess));

			// The column takes any 1000 characters; only this rejects a malformed mailbox, a
			// repeat, or an over-long entry inside the list. See EmailCopyList and the remarks
			// on the EmailProcessDetails entity - the database cannot police a list held in
			// one string, so nothing downstream would catch it either.
			RuleFor(x => x.emailProcess.CCEmail)
				.Must(copyList => EmailCopyList.Validate(copyList) is null)
				.WithMessage(command => EmailCopyList.Validate(command.emailProcess.CCEmail));

			// An active row with no addresses is the one combination that breaks at SEND time
			// rather than here: the send path would hand "" to MimeKit.MailboxAddress.Parse,
			// which throws for the whole notice. Registering an empty list is allowed - it
			// just has to be inactive until somebody fills it in.
			RuleFor(x => x.emailProcess.IsActive)
				.Equal(false)
				.When(x => EmailCopyList.Split(x.emailProcess.CCEmail).Count == 0)
				.WithMessage("A copy list with no email addresses cannot be active.");
		});
	}
}

public class AddEmailProcessHandler : ICommandHandler<AddEmailProcessCommand, AddEmailProcessResult>
{
	private readonly IEmailProcessManagementService _emailProcessManagementService;

	public AddEmailProcessHandler(IEmailProcessManagementService emailProcessManagementService)
	{
		_emailProcessManagementService = emailProcessManagementService;
	}

	public async Task<AddEmailProcessResult> Handle(
		AddEmailProcessCommand request,
		CancellationToken cancellationToken)
	{
		var addedEmailProcess = await _emailProcessManagementService.AddEmailProcessAsync(
			request.emailProcess,
			cancellationToken);

		return new AddEmailProcessResult(addedEmailProcess);
	}
}
