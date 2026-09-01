namespace ATS.Features.Web.BulkUploads.Query.ExportBulkUploadSubjects;

public record ExportBulkUploadSubjectsQueryRequest(Guid FileId)
	: IQuery<ExportBulkUploadSubjectsQueryResult>;

public record ExportBulkUploadSubjectsQueryResult(BulkUploadSubjectExportDTO Export);

public class ExportBulkUploadSubjectsQueryRequestValidator
	: AbstractValidator<ExportBulkUploadSubjectsQueryRequest>
{
	public ExportBulkUploadSubjectsQueryRequestValidator()
	{
		RuleFor(x => x.FileId)
			.NotEmpty()
			.WithMessage("FileId is required.");
	}
}

public class ExportBulkUploadSubjectsHandler
	: IQueryHandler<ExportBulkUploadSubjectsQueryRequest, ExportBulkUploadSubjectsQueryResult>
{
	private readonly IBulkUploadMonitoringService _bulkUploadMonitoringService;

	public ExportBulkUploadSubjectsHandler(
		IBulkUploadMonitoringService bulkUploadMonitoringService)
	{
		_bulkUploadMonitoringService = bulkUploadMonitoringService;
	}

	public async Task<ExportBulkUploadSubjectsQueryResult> Handle(
		ExportBulkUploadSubjectsQueryRequest request,
		CancellationToken cancellationToken)
	{
		var export = await _bulkUploadMonitoringService.ExportSubjectsAsync(
			request.FileId,
			cancellationToken);

		return new ExportBulkUploadSubjectsQueryResult(export);
	}
}
