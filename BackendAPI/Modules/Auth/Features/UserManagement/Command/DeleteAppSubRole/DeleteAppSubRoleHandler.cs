namespace Auth.Features.UserManagement.Command.DeleteAppSubRole;
public record DeleteAppSubRoleCommand(int AppSubRoleId) : ICommand<DeleteAppSubRoleResult>;
public record DeleteAppSubRoleResult(bool IsDeleted);

public class DeleteAppSubRoleCommandValidator : AbstractValidator<DeleteAppSubRoleCommand>
{
	public DeleteAppSubRoleCommandValidator()
	{
		RuleFor(x => x.AppSubRoleId)
			.NotEmpty().WithMessage("AppSubRole with the specified ID does not exist.")
			.GreaterThan(0).WithMessage("AppSubRoleId must be greater than zero.");
	}
}

public class DeleteAppSubRoleHandler : ICommandHandler<DeleteAppSubRoleCommand, DeleteAppSubRoleResult>
{
	private readonly IAppSubRoleService _appSubRoleService;

	public DeleteAppSubRoleHandler(IAppSubRoleService appSubRoleService)
	{
		_appSubRoleService = appSubRoleService;
	}
	public async Task<DeleteAppSubRoleResult> Handle(DeleteAppSubRoleCommand request, CancellationToken cancellationToken)
	{
		var deletedAppSubRole = await _appSubRoleService.DeleteAppSubRoleAsync(request.AppSubRoleId);
		return new DeleteAppSubRoleResult(deletedAppSubRole);
	}
}


