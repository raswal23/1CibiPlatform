namespace Auth.Features.UserManagement.Command.DeleteSubMenu;
public record DeleteSubMenuCommand(int SubMenuId) : ICommand<DeleteSubMenuResult>;
public record DeleteSubMenuResult(bool IsDeleted);

public class DeleteSubMenuCommandValidator : AbstractValidator<DeleteSubMenuCommand>
{
	public DeleteSubMenuCommandValidator()
	{
		RuleFor(x => x.SubMenuId)
			.NotEmpty().WithMessage("AppId is required.")
			.GreaterThan(0).WithMessage("SubMenuId must be greater than zero.");
	}
}

public class DeleteApplicationHandler : ICommandHandler<DeleteSubMenuCommand, DeleteSubMenuResult>
{
	private readonly ISubMenuService _subMenuService;

	public DeleteApplicationHandler(ISubMenuService subMenuService)
	{
		_subMenuService = subMenuService;
	}
	public async Task<DeleteSubMenuResult> Handle(DeleteSubMenuCommand request, CancellationToken cancellationToken)
	{
		var deletedSubMenu = await _subMenuService.DeleteSubMenuAsync(request.SubMenuId);
		return new DeleteSubMenuResult(deletedSubMenu);
	}
}
