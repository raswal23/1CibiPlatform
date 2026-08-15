namespace EmploymentVerification.Features.VerificationRequests.Query.GetSentRequests;

public sealed record GetSentRequestsQuery
	: IQuery<IReadOnlyList<SentVerificationRequestDTO>>;

public sealed class GetSentRequestsHandler(
	IEmploymentVerificationService service)
	: IQueryHandler<
		GetSentRequestsQuery,
		IReadOnlyList<SentVerificationRequestDTO>>
{
	public Task<IReadOnlyList<SentVerificationRequestDTO>> Handle(
		GetSentRequestsQuery request,
		CancellationToken cancellationToken) =>
		service.ListSentRequestsAsync(cancellationToken);
}
