namespace Auth.Features.UserManagement.Command.AddSubMenu;
public record AddSubMenuCommand(AddSubMenuDTO subMenu) : ICommand<AddSubMenuResult>;
public record AddSubMenuResult(bool isAdded);

public class AddSubMenuCommandValidator : AbstractValidator<AddSubMenuCommand>
{
	public AddSubMenuCommandValidator()
	{
		RuleFor(x => x.subMenu)
			.NotNull().WithMessage("SubMenu data is required.");

		When(x => x.subMenu != null, () =>
		{
			RuleFor(x => x.subMenu.SubMenuName)
				.NotEmpty().WithMessage("SubMenu name is required.")
				.MaximumLength(100).WithMessage("SubMenu name must not exceed 100 characters.");
			RuleFor(x => x.subMenu.Description)
				.NotEmpty().WithMessage("Description is required.");
			RuleFor(x => x.subMenu.IsActive)
				.NotEmpty().WithMessage("IsActive is required.");
		});
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

