namespace Auth.Features.UserManagement.Command.EditApplication;
public record EditApplicationCommand(EditApplicationDTO editApplication) : ICommand<EditApplicationResult>;

public class EditApplicationCommandValidator : AbstractValidator<EditApplicationCommand>
{
	public EditApplicationCommandValidator()
	{
		RuleFor(x => x.editApplication)
			.NotNull().WithMessage("Edit application data is required.");

		When(x => x.editApplication != null, () =>
		{
			RuleFor(x => x.editApplication.AppId)
				.NotEmpty().WithMessage("AppId is required.")
				.GreaterThan(0).WithMessage("AppId must be greater than zero.");
			RuleFor(x => x.editApplication.AppName)
				.NotEmpty().WithMessage("AppName is required.")
				.MaximumLength(100).WithMessage("AppName cannot exceed 100 characters.");
			RuleFor(x => x.editApplication.Description)
				.MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");
			RuleFor(x => x.editApplication.IsActive)
				.NotNull().WithMessage("IsActive is required.");
		});
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
