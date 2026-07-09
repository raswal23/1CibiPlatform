namespace Auth.Features.UserManagement.Command.EditSubMenu;
public record EditSubMenuCommand(EditSubMenuDTO editSubMenu) : ICommand<EditSubMenuResult>;

public class EditSubMenuCommandValidator : AbstractValidator<EditSubMenuCommand>
{
	public EditSubMenuCommandValidator()
	{
		RuleFor(x => x.editSubMenu)
			.NotNull().WithMessage("Edit SubMenu data is required.");

		When(x => x.editSubMenu != null, () =>
		{
			RuleFor(x => x.editSubMenu.SubMenuId)
				.NotEmpty().WithMessage("SubMenuId is required.")
				.GreaterThan(0).WithMessage("SubMenuId must be greater than zero.");
			RuleFor(x => x.editSubMenu.SubMenuName)
				.NotEmpty().WithMessage("SubMenuName is required.")
				.MaximumLength(100).WithMessage("SubMenuName cannot exceed 100 characters.");
			RuleFor(x => x.editSubMenu.Description)
				.NotEmpty().WithMessage("Description is required.")
				.MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
			RuleFor(x => x.editSubMenu.IsActive)
				.NotNull().WithMessage("IsActive is required.");
		});
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
