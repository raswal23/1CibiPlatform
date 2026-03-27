namespace Auth.Features.IsChangePasswordTokenValid;

public record IsChangePasswordTokenValidCommand(ForgotPasswordTokenRequestDTO ForgotPasswordTokenRequestDTO) : ICommand<IsChangePasswordTokenValidResult>;

public record IsChangePasswordTokenValidResult(bool IsValid);

public sealed class IsChangePasswordTokenValidValidator : AbstractValidator<IsChangePasswordTokenValidCommand>
{
	public IsChangePasswordTokenValidValidator()
	{
		RuleFor(x => x.ForgotPasswordTokenRequestDTO.tokenHash)
			.NotEmpty().WithMessage("Token is required.");
	}
}

public class IsChangePasswordTokenValidHandler : ICommandHandler<IsChangePasswordTokenValidCommand, IsChangePasswordTokenValidResult>
{
	private readonly IForgotPasswordService _forgotPassword;

	public IsChangePasswordTokenValidHandler(IForgotPasswordService forgotPassword)
	{
		this._forgotPassword = forgotPassword;
	}


	public async Task<IsChangePasswordTokenValidResult> Handle(
		IsChangePasswordTokenValidCommand request,
		CancellationToken cancellationToken)
	{
		var isValid = await _forgotPassword.IsTokenValid(
			request.ForgotPasswordTokenRequestDTO.tokenHash);

		return new IsChangePasswordTokenValidResult(isValid);
	}
}
