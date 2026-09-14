namespace ATS.Features.Web.EmailAccountManagement.Command.RegisterEmailAccount;

public record RegisterEmailAccountCommand(RegisterEmailAccountDTO emailAccount)
	: ICommand<RegisterEmailAccountResult>;

public record RegisterEmailAccountResult(EmailAccountOtpSentDTO otpSent);

public class RegisterEmailAccountCommandValidator : AbstractValidator<RegisterEmailAccountCommand>
{
	public RegisterEmailAccountCommandValidator()
	{
		RuleFor(x => x.emailAccount)
			.NotNull().WithMessage("Email account data is required.");

		When(x => x.emailAccount != null, () =>
		{
			RuleFor(x => x.emailAccount.DisplayName)
				.NotEmpty().WithMessage("Display name is required.")
				.MaximumLength(150).WithMessage("Display name cannot exceed 150 characters.");

			RuleFor(x => x.emailAccount.EmailAddress)
				.NotEmpty().WithMessage("Email address is required.")
				.EmailAddress().WithMessage("Enter a valid email address.")
				.MaximumLength(320).WithMessage("Email address cannot exceed 320 characters.");

			RuleFor(x => x.emailAccount.SmtpHost)
				.NotEmpty().WithMessage("SMTP host is required.")
				.MaximumLength(255).WithMessage("SMTP host cannot exceed 255 characters.");

			RuleFor(x => x.emailAccount.SmtpPort)
				.InclusiveBetween(1, 65535).WithMessage("SMTP port must be between 1 and 65535.");

			RuleFor(x => x.emailAccount.AppPassword)
				.NotEmpty().WithMessage("App password is required.");

			// Lower wins, and 1 is the most preferred. Zero and negatives are rejected rather
			// than normalised so an operator cannot accidentally displace the primary sender.
			RuleFor(x => x.emailAccount.Priority)
				.GreaterThan(0).WithMessage("Priority must be 1 or higher.")
				.When(x => x.emailAccount.Priority.HasValue);

			// The upper bound is a guard against a typo that would let the account run far past
			// any real provider cap before the pre-emptive check ever fires.
			RuleFor(x => x.emailAccount.DailySendLimit)
				.InclusiveBetween(1, 100000).WithMessage("Daily send limit must be between 1 and 100000.")
				.When(x => x.emailAccount.DailySendLimit.HasValue);
		});
	}
}

public class RegisterEmailAccountHandler
	: ICommandHandler<RegisterEmailAccountCommand, RegisterEmailAccountResult>
{
	private readonly IAtsEmailAccountManagementService _emailAccountManagementService;

	public RegisterEmailAccountHandler(IAtsEmailAccountManagementService emailAccountManagementService)
	{
		_emailAccountManagementService = emailAccountManagementService;
	}

	public async Task<RegisterEmailAccountResult> Handle(
		RegisterEmailAccountCommand request,
		CancellationToken cancellationToken)
	{
		var otpSent = await _emailAccountManagementService.RegisterAsync(
			request.emailAccount,
			cancellationToken);

		return new RegisterEmailAccountResult(otpSent);
	}
}
