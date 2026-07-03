namespace Auth.Features.UserManagement.Command.EditSubMenu;
public record EditSubMenuCommand(EditSubMenuDTO editSubMenu) : ICommand<EditSubMenuResult>;

public class EditSubMenuCommandValidator : AbstractValidator<EditSubMenuCommand>
{
	public EditSubMenuCommandValidator()
	{
		RuleFor(x => x.editSubMenu)
			.NotNull().WithMessage("Edit SubMenu data is required.");
	}
}

public record EditSubMenuResult(SubMenuDTO subMenu);
public class EditSubMenuHandler : ICommandHandler<EditSubMenuCommand, EditSubMenuResult>
{

	private readonly ISubMenuService _subMenuService;

	public EditSubMenuHandler(ISubMenuService subMenuService)
	{
		_subMenuService = subMenuService;
	}
	public async Task<EditSubMenuResult> Handle(EditSubMenuCommand request, CancellationToken cancellationToken)
	{
		var editedSubMenu = await _subMenuService.EditSubMenuAsync(request.editSubMenu);
		return new EditSubMenuResult(editedSubMenu);
	}
}
