namespace ATS.Features.PackageManagement.Command.EditPackage;

public record EditPackageCommand(EditPackageDTO editPackage) : ICommand<EditPackageResult>;

public class EditPackageCommandValidator : AbstractValidator<EditPackageCommand>
{
	public EditPackageCommandValidator()
	{
		RuleFor(x => x.editPackage)
			.NotNull().WithMessage("Edit package data is required.");

		When(x => x.editPackage != null, () =>
		{
			RuleFor(x => x.editPackage.PackageId)
				.GreaterThan(0).WithMessage("PackageId is required.");
			RuleFor(x => x.editPackage.PackageName)
				.NotEmpty().WithMessage("PackageName is required.")
				.MaximumLength(255).WithMessage("PackageName cannot exceed 255 characters.");
			RuleFor(x => x.editPackage.PackageDescription)
				.NotEmpty().WithMessage("PackageDescription is required.")
				.MaximumLength(500).WithMessage("PackageDescription cannot exceed 500 characters.");
			RuleFor(x => x.editPackage.IsActive)
				.NotNull().WithMessage("IsActive is required.");
		});
	}
}

public record EditPackageResult(PackageDetailsDTO package);

public class EditPackageHandler : ICommandHandler<EditPackageCommand, EditPackageResult>
{
	private readonly IPackageManagementService _packageManagementService;

	public EditPackageHandler(IPackageManagementService packageManagementService)
	{
		_packageManagementService = packageManagementService;
	}

	public async Task<EditPackageResult> Handle(EditPackageCommand request, CancellationToken cancellationToken)
	{
		var editedPackage = await _packageManagementService.EditPackageAsync(request.editPackage, cancellationToken);
		return new EditPackageResult(editedPackage);
	}
}
