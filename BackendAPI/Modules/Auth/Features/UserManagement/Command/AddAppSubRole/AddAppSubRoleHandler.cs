namespace Auth.Features.UserManagement.Command.AddAppSubRole;
public record AddAppSubRoleCommand(AddAppSubRoleDTO appSubRole) : ICommand<AddAppSubRoleResult>;
public record AddAppSubRoleResult(bool isAdded);

public class AddAppSubRoleCommandValidator : AbstractValidator<AddAppSubRoleCommand>
{
	public AddAppSubRoleCommandValidator()
	{
		RuleFor(x => x.appSubRole)
			.NotNull().WithMessage("AppSubRole data is required.");

		When(x => x.appSubRole != null, () =>
		{
			RuleFor(x => x.appSubRole.UserId)
				.NotEmpty().WithMessage("UserId is required.");
			RuleFor(x => x.appSubRole.AppId)
				.NotEmpty().WithMessage("AppId is required.")
				.GreaterThan(0).WithMessage("AppId must be greater than 0.");
			RuleFor(x => x.appSubRole.SubMenuId)
				.NotEmpty().WithMessage("SubMenuId is required.")
				.GreaterThan(0).WithMessage("SubMenuId must be greater than 0.");
			RuleFor(x => x.appSubRole.RoleId)
				.NotEmpty().WithMessage("RoleId is required.")
				.GreaterThan(0).WithMessage("RoleId must be greater than 0.");
			RuleFor(x => x.appSubRole.AssignedBy)
				.NotEmpty().WithMessage("AssignedBy is required.");
		});
	}
}

public class AddAppSubRoleHandler : ICommandHandler<AddAppSubRoleCommand, AddAppSubRoleResult>
{
	private readonly IAppSubRoleService _appSubRoleService;

	public AddAppSubRoleHandler(IAppSubRoleService appSubRoleService)
	{
		_appSubRoleService = appSubRoleService;
	}
	public async Task<AddAppSubRoleResult> Handle(AddAppSubRoleCommand request, CancellationToken cancellationToken)
	{
		var addedAppSubRole = await _appSubRoleService.AddAppSubRoleAsync(request.appSubRole);
		return new AddAppSubRoleResult(addedAppSubRole);
	}
}




