namespace Auth.Features.UserManagement.Command.DeleteApplication;
public record DeleteApplicationCommand(int AppId) : ICommand<DeleteApplicationResult>;
public record DeleteApplicationResult(bool IsDeleted);

public class DeleteApplicationCommandValidator : AbstractValidator<DeleteApplicationCommand>
{
	public DeleteApplicationCommandValidator()
	{
		RuleFor(x => x.AppId)
			.NotEmpty().WithMessage("AppId is required.")
			.GreaterThan(0).WithMessage("AppId must be greater than zero.");
	}
}

public class DeleteApplicationHandler : ICommandHandler<DeleteApplicationCommand, DeleteApplicationResult>
{
	private readonly IApplicationService _applicationService;

	public DeleteApplicationHandler(IApplicationService applicationService)
	{
		_applicationService = applicationService;
	}
	public async Task<DeleteApplicationResult> Handle(DeleteApplicationCommand request, CancellationToken cancellationToken)
	{
		var deletedApplication = await _applicationService.DeleteApplicationAsync(request.AppId);
		return new DeleteApplicationResult(deletedApplication);
	}
}
