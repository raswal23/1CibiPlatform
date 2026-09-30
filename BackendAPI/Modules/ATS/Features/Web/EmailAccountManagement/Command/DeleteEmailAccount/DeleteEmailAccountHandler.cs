namespace ATS.Features.Web.EmailAccountManagement.Command.DeleteEmailAccount;

public record DeleteEmailAccountCommand(DeleteEmailAccountDTO request)
	: ICommand<DeleteEmailAccountResult>;

/// <summary>
/// The code that was sent, not a deletion. Nothing is removed until it comes back verified.
/// </summary>
public record DeleteEmailAccountResult(EmailAccountOtpSentDTO otpSent);

public class DeleteEmailAccountCommandValidator : AbstractValidator<DeleteEmailAccountCommand>
{
	public DeleteEmailAccountCommandValidator()
	{
		RuleFor(x => x.request)
			.NotNull().WithMessage("Delete data is required.");

		When(x => x.request != null, () =>
		{
			RuleFor(x => x.request.AtsEmailAccountId)
				.GreaterThan(0).WithMessage("A sender account is required.");
		});
	}
}

public class DeleteEmailAccountHandler
	: ICommandHandler<DeleteEmailAccountCommand, DeleteEmailAccountResult>
{
	private readonly IAtsEmailAccountManagementService _emailAccountManagementService;

	public DeleteEmailAccountHandler(IAtsEmailAccountManagementService emailAccountManagementService)
	{
		_emailAccountManagementService = emailAccountManagementService;
	}

	public async Task<DeleteEmailAccountResult> Handle(
		DeleteEmailAccountCommand request,
		CancellationToken cancellationToken)
	{
		var otpSent = await _emailAccountManagementService.DeleteAsync(
			request.request,
			cancellationToken);

		return new DeleteEmailAccountResult(otpSent);
	}
}
