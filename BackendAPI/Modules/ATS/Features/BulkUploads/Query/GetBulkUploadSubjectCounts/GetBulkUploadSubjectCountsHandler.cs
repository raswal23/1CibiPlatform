namespace ATS.Features.BulkUploads.Query.GetBulkUploadSubjectCounts;

public record GetBulkUploadSubjectCountsQueryRequest(
	Guid FileId,
	string? SearchTerm = null)
	: IQuery<GetBulkUploadSubjectCountsQueryResult>;

public record GetBulkUploadSubjectCountsQueryResult(BulkUploadSubjectCountsDTO Counts);

public class GetBulkUploadSubjectCountsQueryRequestValidator
	: AbstractValidator<GetBulkUploadSubjectCountsQueryRequest>
{
	public GetBulkUploadSubjectCountsQueryRequestValidator()
	{
		RuleFor(x => x.FileId)
			.NotEmpty()
			.WithMessage("FileId is required.");
	}
}

public class GetBulkUploadSubjectCountsHandler
	: IQueryHandler<GetBulkUploadSubjectCountsQueryRequest, GetBulkUploadSubjectCountsQueryResult>
{
	private readonly IBulkUploadMonitoringService _bulkUploadMonitoringService;

	public GetBulkUploadSubjectCountsHandler(
		IBulkUploadMonitoringService bulkUploadMonitoringService)
	{
		_bulkUploadMonitoringService = bulkUploadMonitoringService;
	}

	public async Task<GetBulkUploadSubjectCountsQueryResult> Handle(
		GetBulkUploadSubjectCountsQueryRequest request,
		CancellationToken cancellationToken)
	{
		var counts = await _bulkUploadMonitoringService.GetSubjectCountsAsync(
			request.FileId,
			request.SearchTerm,
			cancellationToken);

		return new GetBulkUploadSubjectCountsQueryResult(counts);
	}
}
