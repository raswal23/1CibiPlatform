namespace ATS.Features.Web.EmailAccountManagement.Command.VerifyEmailAccountOtp;

public record VerifyEmailAccountOtpCommand(VerifyEmailAccountOtpDTO verification)
	: ICommand<VerifyEmailAccountOtpResult>;

public record VerifyEmailAccountOtpResult(EmailAccountOtpResultDTO result);

public class VerifyEmailAccountOtpCommandValidator : AbstractValidator<VerifyEmailAccountOtpCommand>
{
	public VerifyEmailAccountOtpCommandValidator()
	{
		RuleFor(x => x.verification)
			.NotNull().WithMessage("Verification data is required.");

		When(x => x.verification != null, () =>
		{
			RuleFor(x => x.verification.AtsEmailAccountId)
				.GreaterThan(0).WithMessage("A sender account is required.");

			RuleFor(x => x.verification.OtpCode)
				.NotEmpty().WithMessage("The verification code is required.")
				.Length(6).WithMessage("The verification code is 6 digits.")
				.Matches("^[0-9]+$").WithMessage("The verification code is 6 digits.");

			// Validated against the known set rather than passed through: the purpose selects
			// which pending change gets applied, so an unrecognised value must not reach the
			// service and match nothing silently.
			RuleFor(x => x.verification.Purpose)
				.NotEmpty().WithMessage("The verification purpose is required.")
				.Must(purpose => purpose is AtsEmailAccountOtpPurpose.Register
					or AtsEmailAccountOtpPurpose.Edit
					or AtsEmailAccountOtpPurpose.Delete)
				.WithMessage("Unknown verification purpose.");
		});
	}
}

public class VerifyEmailAccountOtpHandler
	: ICommandHandler<VerifyEmailAccountOtpCommand, VerifyEmailAccountOtpResult>
{
	private readonly IAtsEmailAccountManagementService _emailAccountManagementService;

	public VerifyEmailAccountOtpHandler(IAtsEmailAccountManagementService emailAccountManagementService)
	{
		_emailAccountManagementService = emailAccountManagementService;
	}

	public async Task<VerifyEmailAccountOtpResult> Handle(
		VerifyEmailAccountOtpCommand request,
		CancellationToken cancellationToken)
	{
		var result = await _emailAccountManagementService.VerifyOtpAsync(
			request.verification,
			cancellationToken);

		return new VerifyEmailAccountOtpResult(result);
	}
}
