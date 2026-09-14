namespace ATS.Features.Web.EmailInvitationRequest;

public record EmailInvitationRequestCommand(EmailInvitationRequestDTO emailInvitationRequestDTO) : ICommand<EmailInvitationRequestResult>;

public record EmailInvitationRequestResult(bool isAdded);

public class EmailInvitationRequestCommandValidator : AbstractValidator<EmailInvitationRequestCommand>
{
	public EmailInvitationRequestCommandValidator()
	{
		RuleFor(x => x.emailInvitationRequestDTO.EmailAddress)
			.NotEmpty()
			.EmailAddress()
			.WithMessage("Email is required.");

		RuleFor(x => x.emailInvitationRequestDTO.FirstName)
			.NotEmpty().WithMessage("First name is required.")
			.MaximumLength(50).WithMessage("First name must not exceed 50 characters.");

		RuleFor(x => x.emailInvitationRequestDTO.LastName)
			.NotEmpty().WithMessage("Last name is required.")
			.MaximumLength(50).WithMessage("Last name must not exceed 50 characters.");

		RuleFor(x => x.emailInvitationRequestDTO.MobileNumber)
			.NotEmpty().WithMessage("Mobile number is required.")
			.Matches(@"^\d{11}$")
			.WithMessage("Mobile Contact Information must be 11 digits.");

		RuleFor(x => x.emailInvitationRequestDTO.SelectPackage)
			.NotEmpty().WithMessage("Package selection is required.")
			.MaximumLength(100).WithMessage("Package selection must not exceed 100 characters.");

		// The package is validated in the service, where the caller's assigned packages
		// are known; only the order type can be checked without a database round trip.
		RuleFor(x => x.emailInvitationRequestDTO.RushNormal)
			.NotEmpty().WithMessage("Rush/Normal selection is required.")
			.Must(orderType => OrderType.Normalize(orderType) is not null)
			.WithMessage($"Rush/Normal selection must be one of: {string.Join(", ", OrderType.All)}.");

		// The web console always sends the chosen screening type; the service then
		// cross-checks it against the selected package's own classification.
		RuleFor(x => x.emailInvitationRequestDTO.AutoChasing)
			.NotNull().WithMessage("Screening type is required.");

		// Data screening sends no application form, so the candidate identity must be
		// captured at order entry. Manual orders collect these through the form instead.
		When(x => x.emailInvitationRequestDTO.AutoChasing == false, () =>
		{
			RuleFor(x => x.emailInvitationRequestDTO.DateOfBirth)
				.NotNull().WithMessage("Date of birth is required for data screening.")
				.Must(dateOfBirth => dateOfBirth < DateOnly.FromDateTime(DateTime.UtcNow))
				.WithMessage("Date of birth must be in the past.");

			RuleFor(x => x.emailInvitationRequestDTO.SSSNumber)
				.NotEmpty().WithMessage("SSS number is required for data screening.")
				.Matches(@"^\d{10}$").WithMessage("SSS number must be 10 digits.");

			RuleFor(x => x.emailInvitationRequestDTO.TINNumber)
				.NotEmpty().WithMessage("TIN number is required for data screening.")
				.Matches(@"^\d{9,12}$").WithMessage("TIN number must be 9 to 12 digits.");
		});
	}
}
public class InsertEmailInvitationRequestHandler : ICommandHandler<EmailInvitationRequestCommand, EmailInvitationRequestResult>
{
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;
	public InsertEmailInvitationRequestHandler(IEndorsementSubmissionService endorsementSubmissionService)
	{
		_endorsementSubmissionService = endorsementSubmissionService;
	}
	public async Task<EmailInvitationRequestResult> Handle(EmailInvitationRequestCommand request, CancellationToken cancellationToken)
	{
		var isAdded = await _endorsementSubmissionService.InsertEmailInvitationRequestAsync(request.emailInvitationRequestDTO, cancellationToken);
		return new EmailInvitationRequestResult(isAdded);
	}
}
