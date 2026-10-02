namespace ATS.Features.Web.EmailAccountManagement.Command.EditEmailAccount;

public record EditEmailAccountCommand(EditEmailAccountDTO emailAccount)
	: ICommand<EditEmailAccountResult>;

/// <summary>
/// <paramref name="otpSent"/> is null when the edit saved outright.
/// </summary>
/// <remarks>
/// That null is the signal the dialog acts on: a null closes the dialog, a value opens the code
/// step. Modelled as a nullable payload rather than a boolean flag so the two cannot disagree.
/// </remarks>
public record EditEmailAccountResult(EmailAccountOtpSentDTO? otpSent);

public class EditEmailAccountCommandValidator : AbstractValidator<EditEmailAccountCommand>
{
	public EditEmailAccountCommandValidator()
	{
		RuleFor(x => x.emailAccount)
			.NotNull().WithMessage("Email account data is required.");

		When(x => x.emailAccount != null, () =>
		{
			RuleFor(x => x.emailAccount.AtsEmailAccountId)
				.GreaterThan(0).WithMessage("A sender account is required.");

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

			RuleFor(x => x.emailAccount.Priority)
				.GreaterThan(0).WithMessage("Priority must be 1 or higher.");

			RuleFor(x => x.emailAccount.DailySendLimit)
				.InclusiveBetween(1, 100000).WithMessage("Daily send limit must be between 1 and 100000.");

			// Only validated when supplied: blank means "keep the stored password", which is the
			// normal case, since the existing one is never sent to the browser to be re-posted.
			RuleFor(x => x.emailAccount.AppPassword)
				.NotEmpty().WithMessage("App password cannot be blank.")
				.When(x => x.emailAccount.AppPassword is not null
					&& x.emailAccount.AppPassword.Length > 0);
		});
	}
}

public class EditEmailAccountHandler : ICommandHandler<EditEmailAccountCommand, EditEmailAccountResult>
{
	private readonly IAtsEmailAccountManagementService _emailAccountManagementService;

	public EditEmailAccountHandler(IAtsEmailAccountManagementService emailAccountManagementService)
	{
		_emailAccountManagementService = emailAccountManagementService;
	}

	public async Task<EditEmailAccountResult> Handle(
		EditEmailAccountCommand request,
		CancellationToken cancellationToken)
	{
		var otpSent = await _emailAccountManagementService.EditAsync(
			request.emailAccount,
			cancellationToken);

		return new EditEmailAccountResult(otpSent);
	}
}
