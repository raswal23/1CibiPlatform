namespace ATS.Features.Web.RoleManagement.Command.AddRole;

public record AddRoleCommand(AddRoleDTO role) : ICommand<AddRoleResult>;

public record AddRoleResult(bool isAdded);

public class AddRoleCommandValidator : AbstractValidator<AddRoleCommand>
{
	public AddRoleCommandValidator()
	{
		RuleFor(x => x.role)
			.NotNull().WithMessage("Role data is required.");

		When(x => x.role != null, () =>
		{
			RuleFor(x => x.role.RoleName)
				.NotEmpty().WithMessage("RoleName is required.")
				.MaximumLength(100).WithMessage("RoleName cannot exceed 100 characters.");
			RuleFor(x => x.role.RoleDescription)
				.NotEmpty().WithMessage("RoleDescription is required.")
				.MaximumLength(500).WithMessage("RoleDescription cannot exceed 500 characters.");
		});
	}
}

public class AddRoleHandler : ICommandHandler<AddRoleCommand, AddRoleResult>
{
	private readonly IRoleManagementService _roleManagementService;

	public AddRoleHandler(IRoleManagementService roleManagementService)
	{
		_roleManagementService = roleManagementService;
	}

	public async Task<AddRoleResult> Handle(AddRoleCommand request, CancellationToken cancellationToken)
	{
		var addedRole = await _roleManagementService.AddRoleAsync(request.role);
		return new AddRoleResult(addedRole);
	}
}
