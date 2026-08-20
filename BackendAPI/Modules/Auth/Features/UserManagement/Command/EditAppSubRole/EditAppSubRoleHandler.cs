namespace Auth.Features.UserManagement.Command.EditAppSubRole;
public record EditAppSubRoleCommand(EditAppSubRoleDTO editAppSubRole) : ICommand<EditAppSubRoleResult>;

public class EditAppSubRoleCommandValidator : AbstractValidator<EditAppSubRoleCommand>
{
	public EditAppSubRoleCommandValidator()
	{
		RuleFor(x => x.editAppSubRole)
			.NotNull().WithMessage("Edit AppSubRole data is required.");

		When(x => x.editAppSubRole != null, () =>
		{
			RuleFor(x => x.editAppSubRole.AppSubRoleId)
				.NotEmpty().WithMessage("AppSubRoleId is required.")
				.GreaterThan(0).WithMessage("AppSubRoleId must be greater than zero.");
			RuleFor(x => x.editAppSubRole.AppId)
				.NotEmpty().WithMessage("AppId is required.")
				.GreaterThan(0).WithMessage("AppId must be greater than zero.");
			RuleFor(x => x.editAppSubRole.UserId)
				.NotEmpty().WithMessage("UserId is required.")
				.Must(userId => userId != Guid.Empty).WithMessage("UserId must be a valid GUID.");
			RuleFor(x => x.editAppSubRole.SubMenuId)
				.NotEmpty().WithMessage("SubMenuId is required.")
				.GreaterThan(0).WithMessage("SubMenuId must be greater than zero.");
			RuleFor(x => x.editAppSubRole.RoleId)
				.NotEmpty().WithMessage("RoleId is required.")
				.GreaterThan(0).WithMessage("RoleId must be greater than zero.");
		});
	}
}

public record EditAppSubRoleResult(AppSubRoleDTO appSubRole);

public class EditAppSubRoleHandler : ICommandHandler<EditAppSubRoleCommand, EditAppSubRoleResult>
{
	private readonly IAppSubRoleService _appSubRoleService;

	public EditAppSubRoleHandler(IAppSubRoleService appSubRoleService)
	{
		_appSubRoleService = appSubRoleService;
	}
	public async Task<EditAppSubRoleResult> Handle(EditAppSubRoleCommand request, CancellationToken cancellationToken)
	{
		var editAppSubRole = await _appSubRoleService.EditAppSubRoleAsync(request.editAppSubRole);
		return new EditAppSubRoleResult(editAppSubRole);
	}
}

