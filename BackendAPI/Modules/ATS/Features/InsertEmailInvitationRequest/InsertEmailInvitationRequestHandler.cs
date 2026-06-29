namespace ATS.Features.EmailInvitationRequest;

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
			.MaximumLength(20).WithMessage("Mobile number must not exceed 20 characters.");

		RuleFor(x => x.emailInvitationRequestDTO.SelectPackage)
			.NotEmpty().WithMessage("Package selection is required.")
			.MaximumLength(100).WithMessage("Package selection must not exceed 100 characters.");

		RuleFor(x => x.emailInvitationRequestDTO.RushNormal)
			.NotEmpty().WithMessage("Rush/Normal selection is required.")
			.MaximumLength(20).WithMessage("Rush/Normal selection must not exceed 20 characters.");

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
