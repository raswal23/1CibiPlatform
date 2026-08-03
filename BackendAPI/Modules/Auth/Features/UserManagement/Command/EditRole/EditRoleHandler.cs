namespace Auth.Features.UserManagement.Command.EditRole;
public record EditRoleCommand(EditRoleDTO editRole) : ICommand<EditRoleResult>;

public class EditRoleCommandValidator : AbstractValidator<EditRoleCommand>
{
	public EditRoleCommandValidator()
	{
		RuleFor(x => x.editRole)
			.NotNull().WithMessage("Edit role data is required.");

		When(x => x.editRole != null, () =>
		{
			RuleFor(x => x.editRole.RoleId)
				.NotEmpty().WithMessage("RoleId is required.")
				.GreaterThan(0).WithMessage("RoleId must be greater than zero.");
			RuleFor(x => x.editRole.RoleName)
				.NotEmpty().WithMessage("RoleName is required.")
				.MaximumLength(100).WithMessage("RoleName must not exceed 100 characters.");
			RuleFor(x => x.editRole.Description)
				.NotEmpty().WithMessage("Description is required.")
				.MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
		});
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



	
