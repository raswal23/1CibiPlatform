namespace ATS.Features.PackageManagement.Command.AddPackage;

public record AddPackageCommand(AddPackageDTO package) : ICommand<AddPackageResult>;

public record AddPackageResult(bool isAdded);

public class AddPackageCommandValidator : AbstractValidator<AddPackageCommand>
{
	public AddPackageCommandValidator()
	{
		RuleFor(x => x.package)
			.NotNull().WithMessage("Package data is required.");

		When(x => x.package != null, () =>
		{
			RuleFor(x => x.package.PackageName)
				.NotEmpty().WithMessage("PackageName is required.")
				.MaximumLength(255).WithMessage("PackageName cannot exceed 255 characters.");
			RuleFor(x => x.package.PackageDescription)
				.NotEmpty().WithMessage("PackageDescription is required.")
				.MaximumLength(500).WithMessage("PackageDescription cannot exceed 500 characters.");
			RuleFor(x => x.package.IsActive)
				.NotNull().WithMessage("IsActive is required.");
		});
	}
}

public class AddPackageHandler : ICommandHandler<AddPackageCommand, AddPackageResult>
{
	private readonly IPackageManagementService _packageManagementService;

	public AddPackageHandler(IPackageManagementService packageManagementService)
	{
		_packageManagementService = packageManagementService;
	}

	public async Task<AddPackageResult> Handle(AddPackageCommand request, CancellationToken cancellationToken)
	{
		var addedPackage = await _packageManagementService.AddPackageAsync(request.package, cancellationToken);
		return new AddPackageResult(addedPackage);
	}
}
