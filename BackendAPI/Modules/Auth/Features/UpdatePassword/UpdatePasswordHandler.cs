namespace Auth.Features.UpdatePassword;

public record UpdatePasswordCommand(UpdatePasswordRequestDTO UpdatePasswordRequestDTO) : ICommand<UpdatePasswordResult>;

public record UpdatePasswordResult(bool IsSuccessful);


public sealed class UpdatePasswordValidator : AbstractValidator<UpdatePasswordCommand>
{
	public UpdatePasswordValidator()
	{
		RuleFor(x => x.UpdatePasswordRequestDTO.hashToken)
			.NotEmpty().WithMessage("Hash token is required.");
		RuleFor(x => x.UpdatePasswordRequestDTO.newPassword)
			.NotEmpty().WithMessage("Password is required.")
			.MinimumLength(6).WithMessage("Password must be at least 6 characters long.")
			.MaximumLength(100).WithMessage("Password must not exceed 100 characters.")
			.Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
			.Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
			.Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
			.Matches(@"[\W_]").WithMessage("Password must contain at least one special character.");
	}
}


public class UpdatePasswordHandler : ICommandHandler<UpdatePasswordCommand, UpdatePasswordResult>
{
	private readonly IForgotPasswordService _forgotPassword;

	public UpdatePasswordHandler(IForgotPasswordService forgotPassword)
	{
		this._forgotPassword = forgotPassword;
	}

	public async Task<UpdatePasswordResult> Handle(UpdatePasswordCommand request, CancellationToken cancellationToken)
	{
		var isUpdated = await this._forgotPassword.ResetPasswordAsync(
			request.UpdatePasswordRequestDTO.hashToken,
			request.UpdatePasswordRequestDTO.newPassword);

		return new UpdatePasswordResult(isUpdated);
	}
}
