namespace EmploymentVerification.Features.VerificationRequests.Query.GetRequests;

public sealed record GetRequestsQuery
	: IQuery<IReadOnlyList<EmploymentVerificationRequest>>;

public sealed class GetRequestsHandler(
	IEmploymentVerificationService service)
	: IQueryHandler<GetRequestsQuery, IReadOnlyList<EmploymentVerificationRequest>>
{
	public Task<IReadOnlyList<EmploymentVerificationRequest>> Handle(
		GetRequestsQuery request,
		CancellationToken cancellationToken) =>
		service.ListAsync(cancellationToken);
}
