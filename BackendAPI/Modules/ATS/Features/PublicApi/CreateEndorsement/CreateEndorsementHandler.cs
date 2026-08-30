namespace ATS.Features.PublicApi.CreateEndorsement;

public record CreateEndorsementCommand(
	string FirstName,
	string LastName,
	string? MiddleInitial,
	string EmailAddress,
	string MobileNumber,
	string Package,
	string OrderType)
	: ICommand<CreateEndorsementResult>;

public record CreateEndorsementResult(bool Success);

public class CreateEndorsementCommandValidator : AbstractValidator<CreateEndorsementCommand>
{
	// Mirrors EmailInvitationRequestCommandValidator: the public API must not accept
	// anything the web console would reject, or the two paths would drift.
	public CreateEndorsementCommandValidator()
	{
		RuleFor(x => x.FirstName)
			.NotEmpty().WithMessage("First name is required.")
			.MaximumLength(50).WithMessage("First name must not exceed 50 characters.");

		RuleFor(x => x.LastName)
			.NotEmpty().WithMessage("Last name is required.")
			.MaximumLength(50).WithMessage("Last name must not exceed 50 characters.");

		// MiddleInitial is deliberately unconstrained beyond length: many subjects have
		// no middle name, so an absent value must never fail the request.
		RuleFor(x => x.MiddleInitial)
			.MaximumLength(255).WithMessage("Middle initial must not exceed 255 characters.");

		RuleFor(x => x.EmailAddress)
			.NotEmpty().WithMessage("Email address is required.")
			.EmailAddress().WithMessage("Email address is not a valid email.");

		RuleFor(x => x.MobileNumber)
			.NotEmpty().WithMessage("Mobile number is required.")
			.Matches(@"^\d{11}$").WithMessage("Mobile number must be 11 digits.");

		RuleFor(x => x.Package)
			.NotEmpty().WithMessage("Package is required.")
			.MaximumLength(100).WithMessage("Package must not exceed 100 characters.");

		RuleFor(x => x.OrderType)
			.NotEmpty().WithMessage("Order type is required.")
			.MaximumLength(20).WithMessage("Order type must not exceed 20 characters.");
	}
}

public class CreateEndorsementHandler : ICommandHandler<CreateEndorsementCommand, CreateEndorsementResult>
{
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;

	public CreateEndorsementHandler(IEndorsementSubmissionService endorsementSubmissionService)
	{
		_endorsementSubmissionService = endorsementSubmissionService;
	}

	public async Task<CreateEndorsementResult> Handle(
		CreateEndorsementCommand request,
		CancellationToken cancellationToken)
	{
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = request.FirstName,
			LastName = request.LastName,
			MiddleInitial = request.MiddleInitial,
			EmailAddress = request.EmailAddress,
			MobileNumber = request.MobileNumber,
			SelectPackage = request.Package,
			RushNormal = request.OrderType
		};

		// Identical to the web path apart from the source, which is what makes an order
		// traceable to the integration that raised it.
		var success = await _endorsementSubmissionService.InsertEmailInvitationRequestAsync(
			dto,
			cancellationToken,
			OrderHistorySource.PublicApi);

		return new CreateEndorsementResult(success);
	}
}
