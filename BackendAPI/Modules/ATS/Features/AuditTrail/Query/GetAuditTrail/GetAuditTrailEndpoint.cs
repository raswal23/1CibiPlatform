namespace ATS.Features.AuditTrail.Query.GetAuditTrail;

public record GetAuditTrailEndpointRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Outcome = null,
	string? Action = null,
	string? Area = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetAuditTrailEndpointResponse(KeysetPaginatedResult<AuditTrailListDTO> AuditEntries);

public class GetAuditTrailEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getaudittrail", async (
			[AsParameters] GetAuditTrailEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetAuditTrailQueryRequest(
				request.Cursor,
				request.PageSize,
				request.Outcome,
				request.Action,
				request.Area,
				request.SearchTerm,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetAuditTrailEndpointResponse(result.AuditEntries));
		})
		.WithName("GetAuditTrail")
		.WithTags("ATS")
		.Produces<GetAuditTrailEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Audit Trail")
		.WithDescription(
			"Retrieves ATS state-changing actions with keyset pagination, optionally "
			+ "filtered by outcome, action, area, user and date range. Restricted to "
			+ "platform super admins; any other caller reads an empty page.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
