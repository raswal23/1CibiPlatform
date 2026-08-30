namespace ATS.Features.Web.BulkUploads.Query.GetBulkUploads;

public record GetBulkUploadsQueryRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Status = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null)
	: IQuery<GetBulkUploadsQueryResult>;

public record GetBulkUploadsQueryResult(KeysetPaginatedResult<BulkUploadListDTO> BulkUploads);

public class GetBulkUploadsQueryRequestValidator : AbstractValidator<GetBulkUploadsQueryRequest>
{
	public GetBulkUploadsQueryRequestValidator()
	{
		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");

		// Cursor is deliberately unvalidated: cursors are opaque and a malformed one
		// self-heals to the first page rather than failing the request.
		RuleFor(x => x.Status)
			.Must(status => string.IsNullOrWhiteSpace(status)
				|| BulkFileStatus.All.Contains(status, StringComparer.OrdinalIgnoreCase))
			.WithMessage($"Status must be empty or one of: {string.Join(", ", BulkFileStatus.All)}.");
	}
}

public class GetBulkUploadsHandler
	: IQueryHandler<GetBulkUploadsQueryRequest, GetBulkUploadsQueryResult>
{
	private readonly IBulkUploadMonitoringService _bulkUploadMonitoringService;

	public GetBulkUploadsHandler(IBulkUploadMonitoringService bulkUploadMonitoringService)
	{
		_bulkUploadMonitoringService = bulkUploadMonitoringService;
	}

	public async Task<GetBulkUploadsQueryResult> Handle(
		GetBulkUploadsQueryRequest request,
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm,
			request.StartDate,
			request.EndDate);

		var bulkUploads = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			paginationRequest,
			request.Status,
			cancellationToken);

		return new GetBulkUploadsQueryResult(bulkUploads);
	}
}
