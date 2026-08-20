namespace EmploymentVerification.Features.VerifyEmployment.Query.GetRequestByToken;

public sealed record GetRequestByTokenQuery(string Token)
	: IQuery<EmploymentVerificationPreviewResult>;

public sealed class GetRequestByTokenQueryValidator
	: AbstractValidator<GetRequestByTokenQuery>
{
	// The emailed link carries the stored SHA-512 hash from IHashService, rendered as
	// unpadded base64url: 86 characters. Rejecting anything else keeps malformed links
	// out of the database lookup.
	private const int TokenLength = 86;

	public GetRequestByTokenQueryValidator()
	{
		RuleFor(query => query.Token)
			.NotEmpty()
			.WithMessage("A verification token is required.")
			.Length(TokenLength)
			.WithMessage("The verification token is malformed.")
			.Matches("^[A-Za-z0-9_-]+$")
			.WithMessage("The verification token is malformed.");
	}
}

public sealed class GetRequestByTokenHandler(
	IEmploymentVerificationService service)
	: IQueryHandler<GetRequestByTokenQuery, EmploymentVerificationPreviewResult>
{
	public Task<EmploymentVerificationPreviewResult> Handle(
		GetRequestByTokenQuery request,
		CancellationToken cancellationToken) =>
		service.GetPreviewByTokenAsync(
			request.Token,
			cancellationToken);
}
