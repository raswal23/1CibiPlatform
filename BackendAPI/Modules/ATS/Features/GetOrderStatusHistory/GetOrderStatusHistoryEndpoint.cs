namespace ATS.Features.GetOrderStatusHistory;

public record GetOrderStatusHistoryResponse(IReadOnlyList<OrderStatusHistoryDTO> History);

public class GetOrderStatusHistoryEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet(
			"getorderstatushistory",
			async (
				Guid emailInvitationRequestId,
				ISender sender,
				CancellationToken cancellationToken) =>
			{
				var query = new GetOrderStatusHistoryQuery(
					emailInvitationRequestId);

				var result = await sender.Send(
					query,
					cancellationToken);

				return Results.Ok(
					new GetOrderStatusHistoryResponse(result.History));
			})
		.WithName("GetOrderStatusHistory")
		.WithTags("ATS")
		.RequireAuthorization();
	}
}
