namespace EmploymentVerification.Features.VerifyEmployment.Command.RejectRequest;

/// <param name="Reason">
/// Why the HR contact says the details are inaccurate. Required: this anonymous
/// endpoint is the only place the reason can ever be captured, and a bare
/// "Verified with inaccuracy" gives whoever picks the order up nothing to act on.
/// </param>
public sealed record RejectRequestCommand(
	string Token,
	string Reason)
	: ICommand<EmploymentVerificationCompletionResult>;

public sealed class RejectRequestCommandValidator
	: AbstractValidator<RejectRequestCommand>
{
	// The emailed link carries the stored SHA-512 hash from IHashService,
	// rendered as unpadded base64url: 86 characters.
	private const int TokenLength = 86;

	// An anonymous external caller writes this straight into a column the console
	// renders in a table cell, so the cap is a storage and layout bound rather than
	// a guess at how much an HR contact needs.
	private const int ReasonMaxLength = 1000;

	public RejectRequestCommandValidator()
	{
		RuleFor(command => command.Token)
			.NotEmpty()
			.WithMessage("A verification token is required.")
			.Length(TokenLength)
			.WithMessage("The verification token is malformed.")
			.Matches("^[A-Za-z0-9_-]+$")
			.WithMessage("The verification token is malformed.");

		RuleFor(command => command.Reason)
			.NotEmpty()
			.WithMessage("Please tell us what is inaccurate about these details.")
			.MaximumLength(ReasonMaxLength)
			.WithMessage($"The reason must be {ReasonMaxLength} characters or fewer.");
	}
}

public sealed class RejectRequestHandler(
	IEmploymentVerificationService service)
	: ICommandHandler<RejectRequestCommand, EmploymentVerificationCompletionResult>
{
	public Task<EmploymentVerificationCompletionResult> Handle(
		RejectRequestCommand request,
		CancellationToken cancellationToken) =>
		service.VerifyAsync(
			request.Token,
			reject: true,
			request.Reason,
			cancellationToken);
}
