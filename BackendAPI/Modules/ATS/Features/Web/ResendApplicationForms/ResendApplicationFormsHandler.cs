namespace ATS.Features.Web.ResendApplicationForms;

public record ResendApplicationFormsCommand(IReadOnlyCollection<Guid> EmailInvitationIds)
	: ICommand<ResendApplicationFormsResult>;

public record ResendApplicationFormsResult(int RequestedCount, int RequeuedCount, bool IsComplete);

public class ResendApplicationFormsCommandValidator : AbstractValidator<ResendApplicationFormsCommand>
{
	public ResendApplicationFormsCommandValidator()
	{
		RuleFor(x => x.EmailInvitationIds)
			.NotNull()
			.WithMessage("At least one invitation is required.")
			.Must(ids => ids is { Count: > 0 })
			.WithMessage("At least one invitation is required.");

		// The cap is enforced in the service too, because that is where the reason for it
		// lives. Validating here turns an oversized request into a 400 before it reaches a
		// database round trip.
		RuleFor(x => x.EmailInvitationIds)
			.Must(ids => ids is null || ids.Count <= EndorsementSubmissionService.MaxBulkResendSize)
			.WithMessage(
				$"A bulk resend is limited to {EndorsementSubmissionService.MaxBulkResendSize} invitations at a time.");

		RuleForEach(x => x.EmailInvitationIds)
			.NotEmpty()
			.WithMessage("Email Invitation ID is required.");
	}
}

public class ResendApplicationFormsHandler
	: ICommandHandler<ResendApplicationFormsCommand, ResendApplicationFormsResult>
{
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;

	public ResendApplicationFormsHandler(IEndorsementSubmissionService endorsementSubmissionService)
	{
		_endorsementSubmissionService = endorsementSubmissionService;
	}

	public async Task<ResendApplicationFormsResult> Handle(
		ResendApplicationFormsCommand request,
		CancellationToken cancellationToken)
	{
		var result = await _endorsementSubmissionService.ResendApplicationFormsAsync(
			request.EmailInvitationIds,
			cancellationToken);

		return new ResendApplicationFormsResult(
			result.RequestedCount,
			result.RequeuedCount,
			result.IsComplete);
	}
}
