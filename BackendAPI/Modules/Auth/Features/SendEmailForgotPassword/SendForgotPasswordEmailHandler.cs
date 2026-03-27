namespace Auth.Features.SendEmailForgotPassword;

public record SendForgotPasswordEmailCommand(
	SendForgotPasswordEmailRequestDTO Request
) : ICommand<SendForgotPasswordEmailResult>;

public record SendForgotPasswordEmailResult(bool IsEmailSent);

public class SendForgotPasswordEmailCommandValidator
	: AbstractValidator<SendForgotPasswordEmailCommand>
{
	public SendForgotPasswordEmailCommandValidator()
	{
		RuleFor(x => x.Request.email)
			.NotEmpty().WithMessage("Email is required.")
			.EmailAddress().WithMessage("Invalid email format.");
	}
}

public class SendForgotPasswordEmailHandler
	: ICommandHandler<SendForgotPasswordEmailCommand, SendForgotPasswordEmailResult>
{
	private readonly IForgotPasswordService _forgotPassword;

	public SendForgotPasswordEmailHandler(IForgotPasswordService forgotPassword)
	{
		_forgotPassword = forgotPassword;
	}

	public async Task<SendForgotPasswordEmailResult> Handle(
		SendForgotPasswordEmailCommand request,
		CancellationToken cancellationToken)
	{
		bool isSent = await _forgotPassword.ForgotPasswordAsync(request.Request.email);

		return new SendForgotPasswordEmailResult(isSent);
	}
}