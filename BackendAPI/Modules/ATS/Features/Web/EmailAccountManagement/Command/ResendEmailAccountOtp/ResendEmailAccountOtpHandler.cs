namespace ATS.Features.Web.EmailAccountManagement.Command.ResendEmailAccountOtp;

public record ResendEmailAccountOtpCommand(ResendEmailAccountOtpDTO request)
	: ICommand<ResendEmailAccountOtpResult>;

public record ResendEmailAccountOtpResult(EmailAccountOtpSentDTO otpSent);

public class ResendEmailAccountOtpCommandValidator : AbstractValidator<ResendEmailAccountOtpCommand>
{
	public ResendEmailAccountOtpCommandValidator()
	{
		RuleFor(x => x.request)
			.NotNull().WithMessage("Resend data is required.");

		When(x => x.request != null, () =>
		{
			RuleFor(x => x.request.AtsEmailAccountId)
				.GreaterThan(0).WithMessage("A sender account is required.");

			RuleFor(x => x.request.Purpose)
				.NotEmpty().WithMessage("The verification purpose is required.")
				.Must(purpose => purpose is AtsEmailAccountOtpPurpose.Register
					or AtsEmailAccountOtpPurpose.Edit
					or AtsEmailAccountOtpPurpose.Delete)
				.WithMessage("Unknown verification purpose.");
		});
	}
}

public class ResendEmailAccountOtpHandler
	: ICommandHandler<ResendEmailAccountOtpCommand, ResendEmailAccountOtpResult>
{
	private readonly IAtsEmailAccountManagementService _emailAccountManagementService;

	public ResendEmailAccountOtpHandler(IAtsEmailAccountManagementService emailAccountManagementService)
	{
		_emailAccountManagementService = emailAccountManagementService;
	}

	public async Task<ResendEmailAccountOtpResult> Handle(
		ResendEmailAccountOtpCommand request,
		CancellationToken cancellationToken)
	{
		var otpSent = await _emailAccountManagementService.ResendOtpAsync(
			request.request,
			cancellationToken);

		return new ResendEmailAccountOtpResult(otpSent);
	}
}
