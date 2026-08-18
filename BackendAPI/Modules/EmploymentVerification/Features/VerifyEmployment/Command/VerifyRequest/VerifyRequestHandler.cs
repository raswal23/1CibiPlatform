namespace EmploymentVerification.Features.VerifyEmployment.Command.VerifyRequest;

public sealed record VerifyRequestCommand(string Token)
	: ICommand<EmploymentVerificationCompletionResult>;

public sealed class VerifyRequestCommandValidator
	: AbstractValidator<VerifyRequestCommand>
{
	// The emailed link carries the stored SHA-512 hash from IHashService,
	// rendered as unpadded base64url: 86 characters.
	private const int TokenLength = 86;

	public VerifyRequestCommandValidator()
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

public sealed class VerifyRequestHandler(
	IEmploymentVerificationService service)
	: ICommandHandler<VerifyRequestCommand, EmploymentVerificationCompletionResult>
{
	public Task<EmploymentVerificationCompletionResult> Handle(
		VerifyRequestCommand request,
		CancellationToken cancellationToken) =>
		service.VerifyAsync(
			request.Token,
			reject: false,
			cancellationToken);
}
