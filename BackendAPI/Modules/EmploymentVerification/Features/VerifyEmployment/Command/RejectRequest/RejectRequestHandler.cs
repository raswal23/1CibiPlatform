namespace EmploymentVerification.Features.VerifyEmployment.Command.RejectRequest;

public sealed record RejectRequestCommand(string Token)
	: ICommand<EmploymentVerificationCompletionResult>;

public sealed class RejectRequestCommandValidator
	: AbstractValidator<RejectRequestCommand>
{
	// The emailed link carries the stored SHA-512 hash from IHashService,
	// rendered as unpadded base64url: 86 characters.
	private const int TokenLength = 86;

	public RejectRequestCommandValidator()
	{
		RuleFor(command => command.Token)
			.NotEmpty()
			.WithMessage("A verification token is required.")
			.Length(TokenLength)
			.WithMessage("The verification token is malformed.")
			.Matches("^[A-Za-z0-9_-]+$")
			.WithMessage("The verification token is malformed.");
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
			cancellationToken);
}
