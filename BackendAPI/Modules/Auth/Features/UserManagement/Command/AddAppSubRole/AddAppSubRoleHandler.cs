namespace Auth.Features.UserManagement.Command.AddAppSubRole;
public record AddAppSubRoleCommand(AddAppSubRoleDTO appSubRole) : ICommand<AddAppSubRoleResult>;
public record AddAppSubRoleResult(bool isAdded);

public class AddAppSubRoleCommandValidator : AbstractValidator<AddAppSubRoleCommand>
{
	public AddAppSubRoleCommandValidator()
	{
		RuleFor(x => x.appSubRole)
			.NotNull().WithMessage("AppSubRole data is required.");
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




