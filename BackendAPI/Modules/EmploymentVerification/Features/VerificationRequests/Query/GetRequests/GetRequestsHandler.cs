namespace EmploymentVerification.Features.VerificationRequests.Query.GetRequests;

// Returns the safe projection, not the entity. EmploymentVerificationRequest carries
// VerificationTokenHash, which is the credential embedded in the emailed link - see
// SentVerificationRequestDTO's remarks. Serialising the entity here would hand every
// authenticated caller a working verification link for every pending request.
public sealed record GetRequestsQuery
	: IQuery<IReadOnlyList<SentVerificationRequestDTO>>;

public sealed class GetRequestsHandler(
	IEmploymentVerificationService service)
	: IQueryHandler<
		GetRequestsQuery,
		IReadOnlyList<SentVerificationRequestDTO>>
{
	public Task<IReadOnlyList<SentVerificationRequestDTO>> Handle(
		GetRequestsQuery request,
		CancellationToken cancellationToken) =>
		service.ListSentRequestsAsync(cancellationToken);
}
