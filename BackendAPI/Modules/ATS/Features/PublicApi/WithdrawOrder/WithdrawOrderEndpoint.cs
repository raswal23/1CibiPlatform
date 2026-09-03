namespace ATS.Features.PublicApi.WithdrawOrder;

public record WithdrawOrderEndpointResponse(bool Success);

public class WithdrawOrderEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("api/public/ats/orders/{orderId:guid}/withdraw", async (
			Guid orderId,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new WithdrawOrderCommand(orderId);

			var result = await sender.Send(command, cancellationToken);

			return Results.Ok(new WithdrawOrderEndpointResponse(result.Success).Success);
		})
		.WithName("PublicWithdrawOrder")
		.WithTags("ATS Public API")
		.Produces<bool>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.ProducesProblem(StatusCodes.Status409Conflict)
		.WithSummary("Withdraw an order")
		.WithDescription(
			"Withdraws an order the access token's client owns, stopping any further "
			+ "processing. Returns 404 when the order is not theirs, and 409 when it is "
			+ "already withdrawn or completed.")
		.RequireAuthorization();
	}
}
