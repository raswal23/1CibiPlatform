namespace ATS.Features.PublicApi.GetOrder;

public record GetOrderEndpointResponse(PublicOrderDetailDTO Order);

public class GetOrderEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("api/public/ats/orders/{orderId:guid}", async (
			Guid orderId,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetOrderQueryRequest(orderId);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetOrderEndpointResponse(result.Order));
		})
		.WithName("PublicGetOrder")
		.WithTags("ATS Public API")
		.Produces<GetOrderEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get an order")
		.WithDescription(
			"Returns one order's current status, its OMS ticket number once raised, and "
			+ "its event history. Returns 404 when the order does not belong to the "
			+ "access token's client.")
		.RequireAuthorization();
	}
}
