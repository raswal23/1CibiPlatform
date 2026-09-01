namespace ATS.Features.Web.OMSTicketing.Query.GetTicketStatusCounts;

public record GetTicketStatusCountsEndpointRequest(
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetTicketStatusCountsEndpointResponse(TicketStatusCountsDTO Counts);

public class GetTicketStatusCountsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getticketstatuscounts", async (
			[AsParameters] GetTicketStatusCountsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetTicketStatusCountsQueryRequest(
				request.SearchTerm,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetTicketStatusCountsEndpointResponse(result.Counts));
		})
		.WithName("GetTicketStatusCounts")
		.WithTags("ATS")
		.Produces<GetTicketStatusCountsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Ticket Status Counts")
		.WithDescription(
			"Returns how many of the caller's orders are Pending, Processing, Done and "
			+ "Error for OMS ticketing. Honours the search and date filters but never the "
			+ "selected status, so every bucket keeps reporting its own size.")
		.RequireAuthorization();
	}
}
