namespace ATS.Features.BulkUploads.Query.GetBulkUploadStatusCounts;

public record GetBulkUploadStatusCountsQueryRequest(
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null)
	: IQuery<GetBulkUploadStatusCountsQueryResult>;

public record GetBulkUploadStatusCountsQueryResult(BulkUploadStatusCountsDTO Counts);

public class GetBulkUploadStatusCountsHandler
	: IQueryHandler<GetBulkUploadStatusCountsQueryRequest, GetBulkUploadStatusCountsQueryResult>
{
	private readonly IBulkUploadMonitoringService _bulkUploadMonitoringService;

	public GetBulkUploadStatusCountsHandler(IBulkUploadMonitoringService bulkUploadMonitoringService)
	{
		_bulkUploadMonitoringService = bulkUploadMonitoringService;
	}

	public async Task<GetBulkUploadStatusCountsQueryResult> Handle(
		GetBulkUploadStatusCountsQueryRequest request,
		CancellationToken cancellationToken)
	{
		var counts = await _bulkUploadMonitoringService.GetStatusCountsAsync(
			request.SearchTerm,
			request.StartDate,
			request.EndDate,
			cancellationToken);

		return new GetBulkUploadStatusCountsQueryResult(counts);
	}
}
