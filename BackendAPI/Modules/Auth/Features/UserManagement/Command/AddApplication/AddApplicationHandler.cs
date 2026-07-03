namespace Auth.Features.UserManagement.Command.AddApplication;
public record AddApplicationCommand(AddApplicationDTO application) : ICommand<AddApplicationResult>;
public record AddApplicationResult(bool isAdded);

public class AddApplicationCommandValidator : AbstractValidator<AddApplicationCommand>
{
	public AddApplicationCommandValidator()
	{
		RuleFor(x => x.application)
			.NotNull().WithMessage("Application data is required.");
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

