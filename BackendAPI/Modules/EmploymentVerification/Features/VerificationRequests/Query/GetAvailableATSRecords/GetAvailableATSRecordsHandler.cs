namespace EmploymentVerification.Features.VerificationRequests.Query.GetAvailableATSRecords;

public sealed record GetAvailableATSRecordsQuery
	: IQuery<IReadOnlyList<ATSInProgressEmploymentRecord>>;

public sealed class GetAvailableATSRecordsHandler(
	IEmploymentVerificationService service)
	: IQueryHandler<
		GetAvailableATSRecordsQuery,
		IReadOnlyList<ATSInProgressEmploymentRecord>>
{
	public Task<IReadOnlyList<ATSInProgressEmploymentRecord>> Handle(
		GetAvailableATSRecordsQuery request,
		CancellationToken cancellationToken) =>
			service.GetAvailableATSRecordsAsync(cancellationToken);
}
