namespace ATS.Features.AuditTrail.Query.GetAuditOutcomeCounts;

public record GetAuditOutcomeCountsEndpointRequest(
	string? Action = null,
	string? Area = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetAuditOutcomeCountsEndpointResponse(AuditOutcomeCountsDTO Counts);

public class GetAuditOutcomeCountsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getauditoutcomecounts", async (
			[AsParameters] GetAuditOutcomeCountsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetAuditOutcomeCountsQueryRequest(
				request.Action,
				request.Area,
				request.SearchTerm,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetAuditOutcomeCountsEndpointResponse(result.Counts));
		})
		.WithName("GetAuditOutcomeCounts")
		.WithTags("ATS")
		.Produces<GetAuditOutcomeCountsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Audit Outcome Counts")
		.WithDescription(
			"Returns the number of successful and failed ATS actions for the current "
			+ "filters, so the audit trail chips keep showing every bucket's size while "
			+ "one of them is selected.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
