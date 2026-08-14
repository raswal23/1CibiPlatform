namespace Auth.Features.UserManagement.Command.DeleteRole;
public record DeleteRoleCommand(int RoleId) : ICommand<DeleteRoleResult>;
public record DeleteRoleResult(bool IsDeleted);

public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
			.NotEmpty().WithMessage("AppId is required.")
			.GreaterThan(0).WithMessage("RoleId must be greater than zero.");
    }
}

public class DeleteRoleHandler : ICommandHandler<DeleteRoleCommand, DeleteRoleResult>
{
    private readonly IRoleService _roleService;

    public DeleteRoleHandler(IRoleService roleService)
    {
        _roleService = roleService;
    }
    public async Task<DeleteRoleResult> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var deletedRole = await _roleService.DeleteRoleAsync(request.RoleId);
        return new DeleteRoleResult(deletedRole);
    }
}



