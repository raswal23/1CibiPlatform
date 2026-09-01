namespace ATS.Features.Web.ModuleManagement.Command.AddModule;

public record AddModuleCommand(AddModuleDTO module) : ICommand<AddModuleResult>;

public record AddModuleResult(bool isAdded);

public class AddModuleCommandValidator : AbstractValidator<AddModuleCommand>
{
	public AddModuleCommandValidator()
	{
		RuleFor(x => x.module)
			.NotNull().WithMessage("Module data is required.");

		When(x => x.module != null, () =>
		{
			RuleFor(x => x.module.ModuleName)
				.NotEmpty().WithMessage("ModuleName is required.")
				.MaximumLength(100).WithMessage("ModuleName cannot exceed 100 characters.");
			RuleFor(x => x.module.ModuleDescription)
				.NotEmpty().WithMessage("ModuleDescription is required.")
				.MaximumLength(500).WithMessage("ModuleDescription cannot exceed 500 characters.");
		});
	}
}

public class AddModuleHandler : ICommandHandler<AddModuleCommand, AddModuleResult>
{
	private readonly IModuleManagementService _moduleManagementService;

	public AddModuleHandler(IModuleManagementService moduleManagementService)
	{
		_moduleManagementService = moduleManagementService;
	}

	public async Task<AddModuleResult> Handle(AddModuleCommand request, CancellationToken cancellationToken)
	{
		var addedModule = await _moduleManagementService.AddModuleAsync(request.module);
		return new AddModuleResult(addedModule);
	}
}
