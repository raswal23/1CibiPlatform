namespace Auth.Features.UserManagement.Command.EditRole;
public record EditRoleCommand(EditRoleDTO editRole) : ICommand<EditRoleResult>;

public class EditRoleCommandValidator : AbstractValidator<EditRoleCommand>
{
	public EditRoleCommandValidator()
	{
		RuleFor(x => x.editRole)
			.NotNull().WithMessage("Edit role data is required.");
	}
}

public record EditRoleResult(RoleDTO role);
public class EditRoleHandler : ICommandHandler<EditRoleCommand, EditRoleResult>
{
	private readonly IRoleService _roleService;

	public EditRoleHandler(IRoleService roleService)
	{
		_roleService = roleService;
	}
	public async Task<EditRoleResult> Handle(EditRoleCommand request, CancellationToken cancellationToken)
	{
		var editedRole = await _roleService.EditRoleAsync(request.editRole);
		return new EditRoleResult(editedRole);
	}
}



	
