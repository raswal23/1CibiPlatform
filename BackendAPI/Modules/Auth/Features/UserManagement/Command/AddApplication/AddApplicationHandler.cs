namespace Auth.Features.UserManagement.Command.AddApplication;
public record AddApplicationCommand(AddApplicationDTO application) : ICommand<AddApplicationResult>;
public record AddApplicationResult(bool isAdded);

public class AddApplicationCommandValidator : AbstractValidator<AddApplicationCommand>
{
	public AddApplicationCommandValidator()
	{
		RuleFor(x => x.application)
			.NotNull().WithMessage("Application data is required.");

		When(x => x.application != null, () =>
		{
			RuleFor(x => x.application.AppName)
				.NotEmpty().WithMessage("AppName is required.");
			RuleFor(x => x.application.Description)
				.NotEmpty().WithMessage("AppDescription is required.");
			RuleFor(x => x.application.IsActive)
				.NotEmpty().WithMessage("IsActive is required.");
		});
	}
}

public class AddApplicationHandler : ICommandHandler<AddApplicationCommand, AddApplicationResult>
{
	private readonly IApplicationService _applicationService;

	public AddApplicationHandler(IApplicationService applicationService)
	{
		_applicationService = applicationService;
	}
	public async Task<AddApplicationResult> Handle(AddApplicationCommand request, CancellationToken cancellationToken)
	{
		var addedApplication = await _applicationService.AddApplicationAsync(request.application);
		return new AddApplicationResult(addedApplication);
	}
}

