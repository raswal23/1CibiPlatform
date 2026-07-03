namespace Auth.Features.AccountAssignmentNotification;
public record AccountNotificationCommand(AccountNotificationDTO AccountNotificationDTO) : ICommand<AccountNotificationResult>;
public record AccountNotificationResult(bool IsSent);

public class AccountNotificationCommandValidator : AbstractValidator<AccountNotificationCommand>
{
	public AccountNotificationCommandValidator()
	{
		RuleFor(x => x.AccountNotificationDTO)
			.NotNull().WithMessage("Account notification data is required.");
	}
}

public class AccountAssignmentNotificationHandler : ICommandHandler<AccountNotificationCommand, AccountNotificationResult>
{
	private readonly IAppSubRoleService _appSubRoleService;
	public AccountAssignmentNotificationHandler(IAppSubRoleService appSubRoleService)
	{
		_appSubRoleService = appSubRoleService;
	}

	public async Task<AccountNotificationResult> Handle(AccountNotificationCommand request, CancellationToken cancellationToken)
	{

		var result = await _appSubRoleService.SendToUserEmailAsync(request.AccountNotificationDTO);

		return new AccountNotificationResult(result);
	}
}
