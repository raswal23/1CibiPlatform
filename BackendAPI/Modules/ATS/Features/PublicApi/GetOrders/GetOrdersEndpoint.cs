namespace ATS.Features.PublicApi.GetOrders;

public record GetOrdersEndpointRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetOrdersEndpointResponse(KeysetPaginatedResult<ReportListDTO> Orders);

public class GetOrdersEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("api/public/ats/orders", async (
			[AsParameters] GetOrdersEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetOrdersQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetOrdersEndpointResponse(result.Orders));
		})
		.WithName("PublicGetOrders")
		.WithTags("ATS Public API")
		.Produces<GetOrdersEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.WithSummary("List orders")
		.WithDescription(
			"Returns the orders belonging to the access token's client, newest first, "
			+ "with keyset pagination. Pass the returned cursor to fetch the next page.")
		.RequireAuthorization();
	}
}
