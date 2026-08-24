namespace ATS.Features.DisputeOrder;

public record GetDisputeOrdersEndpointRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null);

public record GetDisputeOrdersEndpointResponse(KeysetPaginatedResult<DisputeOrderListDTO> Orders);

public class GetDisputeOrdersEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getdisputeorders", async (
			[AsParameters] GetDisputeOrdersEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetDisputeOrdersQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetDisputeOrdersEndpointResponse(result.Orders));
		})
		.WithName("GetDisputeOrders")
		.WithTags("ATS")
		.Produces<GetDisputeOrdersEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Dispute Orders")
		.WithDescription("Retrieves dispute-eligible orders: completed orders within last 5 days or orders with disputed status.")
		.RequireAuthorization();
	}
}
