namespace Auth.Features.UserManagement.Command.EditAppSubRole;
public record EditAppSubRoleCommand(EditAppSubRoleDTO editAppSubRole) : ICommand<EditAppSubRoleResult>;

public class EditAppSubRoleCommandValidator : AbstractValidator<EditAppSubRoleCommand>
{
	public EditAppSubRoleCommandValidator()
	{
		RuleFor(x => x.editAppSubRole)
			.NotNull().WithMessage("Edit AppSubRole data is required.");
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

