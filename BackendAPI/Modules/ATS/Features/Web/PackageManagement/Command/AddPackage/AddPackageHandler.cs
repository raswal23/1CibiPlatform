namespace ATS.Features.Web.PackageManagement.Command.AddPackage;

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

			// Days after the order that the follow-up reminder is sent; 0 turns it off.
			// Bounded because the chaser fires on OrderCreatedAt + this interval, and a
			// mistyped 900 is indistinguishable from "never" until three years from now.
			RuleFor(x => x.package.FollowUpEmail)
				.InclusiveBetween(0, 90).WithMessage("Follow-up must be between 0 and 90 days.");
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
