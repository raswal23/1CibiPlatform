namespace Auth.Features.UserManagement.Command.AddSubMenu;
public record AddSubMenuCommand(AddSubMenuDTO subMenu) : ICommand<AddSubMenuResult>;
public record AddSubMenuResult(bool isAdded);

public class AddSubMenuCommandValidator : AbstractValidator<AddSubMenuCommand>
{
	public AddSubMenuCommandValidator()
	{
		RuleFor(x => x.subMenu)
			.NotNull().WithMessage("SubMenu data is required.");
	}
}

public class AddSubMenuHandler : ICommandHandler<AddSubMenuCommand, AddSubMenuResult>
{
	private readonly ISubMenuService _subMenuService;

	public AddSubMenuHandler(ISubMenuService subMenuService)
	{
		_subMenuService = subMenuService;
	}
	public async Task<AddSubMenuResult> Handle(AddSubMenuCommand request, CancellationToken cancellationToken)
	{
		var addedSubMenu = await _subMenuService.AddSubMenuAsync(request.subMenu);
		return new AddSubMenuResult(addedSubMenu);
	}
}

