namespace ATS.Features.Web.RoleManagement.Command.EditRole;

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
				.GreaterThan(0).WithMessage("RoleId must be greater than zero.");
			RuleFor(x => x.editRole.RoleName)
				.NotEmpty().WithMessage("RoleName is required.")
				.MaximumLength(100).WithMessage("RoleName cannot exceed 100 characters.");
			RuleFor(x => x.editRole.RoleDescription)
				.NotEmpty().WithMessage("RoleDescription is required.")
				.MaximumLength(500).WithMessage("RoleDescription cannot exceed 500 characters.");
		});
	}
}

public record EditRoleResult(RoleDetailsDTO role);

public class EditRoleHandler : ICommandHandler<EditRoleCommand, EditRoleResult>
{
	private readonly IRoleManagementService _roleManagementService;

	public EditRoleHandler(IRoleManagementService roleManagementService)
	{
		_roleManagementService = roleManagementService;
	}

	public async Task<EditRoleResult> Handle(EditRoleCommand request, CancellationToken cancellationToken)
	{
		var editedRole = await _roleManagementService.EditRoleAsync(request.editRole);
		return new EditRoleResult(editedRole);
	}
}
