namespace Auth.Features.UserManagement.Command.EditApplication;
public record EditApplicationCommand(EditApplicationDTO editApplication) : ICommand<EditApplicationResult>;

public class EditApplicationCommandValidator : AbstractValidator<EditApplicationCommand>
{
	public EditApplicationCommandValidator()
	{
		RuleFor(x => x.editApplication)
			.NotNull().WithMessage("Edit application data is required.");
	}
}

public record EditApplicationResult(ApplicationDTO application);
public class EditApplicationHandler : ICommandHandler<EditApplicationCommand, EditApplicationResult>
{

	private readonly IApplicationService _applicationService;

	public EditApplicationHandler(IApplicationService applicationService)
	{
		_applicationService = applicationService;
	}
	public async Task<EditApplicationResult> Handle(EditApplicationCommand request, CancellationToken cancellationToken)
	{
		var editedApplication = await _applicationService.EditApplicationAsync(request.editApplication);
		return new EditApplicationResult(editedApplication);
	}
}
