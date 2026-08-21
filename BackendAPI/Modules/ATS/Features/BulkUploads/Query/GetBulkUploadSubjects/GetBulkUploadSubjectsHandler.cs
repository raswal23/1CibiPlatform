namespace ATS.Features.BulkUploads.Query.GetBulkUploadSubjects;

public record GetBulkUploadSubjectsQueryRequest(
	Guid FileId,
	string? Cursor = null,
	int? PageSize = 10,
	string? EmailStatus = null,
	string? SearchTerm = null)
	: IQuery<GetBulkUploadSubjectsQueryResult>;

public record GetBulkUploadSubjectsQueryResult(BulkUploadSubjectsResultDTO Result);

public class GetBulkUploadSubjectsQueryRequestValidator
	: AbstractValidator<GetBulkUploadSubjectsQueryRequest>
{
	public GetBulkUploadSubjectsQueryRequestValidator()
	{
		RuleFor(x => x.FileId)
			.NotEmpty()
			.WithMessage("FileId is required.");

		RuleFor(x => x.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= 100))
			.WithMessage("PageSize must be greater than 0 and less than or equal to 100.");

		// Cursor is deliberately unvalidated: cursors are opaque and a malformed one
		// self-heals to the first page rather than failing the request.
		RuleFor(x => x.EmailStatus)
			.Must(emailStatus => string.IsNullOrWhiteSpace(emailStatus)
				|| BulkSubjectEmailStatus.All.Contains(emailStatus, StringComparer.OrdinalIgnoreCase))
			.WithMessage(
				$"EmailStatus must be empty or one of: {string.Join(", ", BulkSubjectEmailStatus.All)}.");
	}
}

public class GetBulkUploadSubjectsHandler
	: IQueryHandler<GetBulkUploadSubjectsQueryRequest, GetBulkUploadSubjectsQueryResult>
{
	private readonly IBulkUploadMonitoringService _bulkUploadMonitoringService;

	public GetBulkUploadSubjectsHandler(IBulkUploadMonitoringService bulkUploadMonitoringService)
	{
		_bulkUploadMonitoringService = bulkUploadMonitoringService;
	}

	public async Task<GetBulkUploadSubjectsQueryResult> Handle(
		GetBulkUploadSubjectsQueryRequest request,
		CancellationToken cancellationToken)
	{
		// StartDate/EndDate are unused here: a subject inherits its file's upload date,
		// so a date range inside one file would filter nothing.
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var result = await _bulkUploadMonitoringService.GetSubjectsAsync(
			request.FileId,
			paginationRequest,
			request.EmailStatus,
			cancellationToken);

		return new GetBulkUploadSubjectsQueryResult(result);
	}
}
