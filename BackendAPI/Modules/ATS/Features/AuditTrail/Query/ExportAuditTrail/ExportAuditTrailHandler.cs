namespace ATS.Features.AuditTrail.Query.ExportAuditTrail;

public record ExportAuditTrailQueryRequest(
	string? Outcome,
	string? Action,
	string? Area,
	string? SearchTerm,
	DateTime? StartDate,
	DateTime? EndDate) : IQuery<ExportAuditTrailQueryResult>;

public record ExportAuditTrailQueryResult(AtsAuditExportDTO Export);

public class ExportAuditTrailQueryRequestValidator
	: AbstractValidator<ExportAuditTrailQueryRequest>
{
	public ExportAuditTrailQueryRequestValidator()
	{
		// An inverted range is a caller mistake that would otherwise render an empty
		// workbook and look like "no activity".
		RuleFor(request => request)
			.Must(request =>
				!request.StartDate.HasValue
				|| !request.EndDate.HasValue
				|| request.StartDate.Value.Date <= request.EndDate.Value.Date)
			.WithMessage("The start date must be on or before the end date.");
	}
}

public class ExportAuditTrailHandler
	: IQueryHandler<ExportAuditTrailQueryRequest, ExportAuditTrailQueryResult>
{
	private readonly IAtsAuditService _auditService;

	public ExportAuditTrailHandler(IAtsAuditService auditService)
	{
		_auditService = auditService;
	}

	public async Task<ExportAuditTrailQueryResult> Handle(
		ExportAuditTrailQueryRequest request,
		CancellationToken cancellationToken)
	{
		var export = await _auditService.ExportAuditTrailAsync(
			request.Outcome,
			request.Action,
			request.Area,
			request.SearchTerm,
			request.StartDate,
			request.EndDate,
			cancellationToken);

		return new ExportAuditTrailQueryResult(export);
	}
}
