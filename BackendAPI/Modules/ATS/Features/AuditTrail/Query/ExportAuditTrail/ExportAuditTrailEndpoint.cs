namespace ATS.Features.AuditTrail.Query.ExportAuditTrail;

public record ExportAuditTrailEndpointRequest(
	string? Outcome = null,
	string? Action = null,
	string? Area = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public class ExportAuditTrailEndpoint : ICarterModule
{
	private const string ExcelContentType =
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("exportaudittrail", async (
			[AsParameters] ExportAuditTrailEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new ExportAuditTrailQueryRequest(
				request.Outcome,
				request.Action,
				request.Area,
				request.SearchTerm,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			// The filename is built by the service from a timestamp, never from caller
			// input, so a filter value can never reach the Content-Disposition header.
			return Results.File(
				result.Export.Content,
				ExcelContentType,
				result.Export.FileName);
		})
		.WithName("ExportAuditTrail")
		.WithTags("ATS")
		.Produces(StatusCodes.Status200OK, contentType: ExcelContentType)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.WithSummary("Export Audit Trail")
		.WithDescription(
			"Downloads the filtered audit trail as a styled Excel workbook: failed rows are "
			+ "highlighted and the cause of each failure has its own column, with a frozen "
			+ "header and auto-filter. Restricted to platform super admins - unlike the "
			+ "paged read, any other caller receives 403 rather than an empty file.")
		.RequireAuthorization();
	}
}
