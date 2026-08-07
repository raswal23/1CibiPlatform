namespace ATS.Features.ModuleManagement.Command.EditModule;

public record EditModuleCommand(EditModuleDTO editModule) : ICommand<EditModuleResult>;

public class EditModuleCommandValidator : AbstractValidator<EditModuleCommand>
{
	public EditModuleCommandValidator()
	{
		RuleFor(x => x.editModule)
			.NotNull().WithMessage("Edit module data is required.");

		When(x => x.editModule != null, () =>
		{
			RuleFor(x => x.editModule.ModuleId)
				.GreaterThan(0).WithMessage("ModuleId must be greater than zero.");
			RuleFor(x => x.editModule.ModuleName)
				.NotEmpty().WithMessage("ModuleName is required.")
				.MaximumLength(100).WithMessage("ModuleName cannot exceed 100 characters.");
			RuleFor(x => x.editModule.ModuleDescription)
				.NotEmpty().WithMessage("ModuleDescription is required.")
				.MaximumLength(500).WithMessage("ModuleDescription cannot exceed 500 characters.");
		});
	}
}

public record EditModuleResult(ModuleDetailsDTO module);

public class EditModuleHandler : ICommandHandler<EditModuleCommand, EditModuleResult>
{
	private readonly IModuleManagementService _moduleManagementService;

	public EditModuleHandler(IModuleManagementService moduleManagementService)
	{
		_moduleManagementService = moduleManagementService;
	}

	public async Task<EditModuleResult> Handle(EditModuleCommand request, CancellationToken cancellationToken)
	{
		var editedModule = await _moduleManagementService.EditModuleAsync(request.editModule);
		return new EditModuleResult(editedModule);
	}
}
