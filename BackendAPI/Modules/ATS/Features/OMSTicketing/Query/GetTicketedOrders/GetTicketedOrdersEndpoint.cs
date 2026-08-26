namespace ATS.Features.OMSTicketing.Query.GetTicketedOrders;

public record GetTicketedOrdersEndpointRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Status = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetTicketedOrdersEndpointResponse(KeysetPaginatedResult<TicketedOrderListDTO> TicketedOrders);

public class GetTicketedOrdersEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getticketedorders", async (
			[AsParameters] GetTicketedOrdersEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetTicketedOrdersQueryRequest(
				request.Cursor,
				request.PageSize,
				request.Status,
				request.SearchTerm,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetTicketedOrdersEndpointResponse(result.TicketedOrders));
		})
		.WithName("GetTicketedOrders")
		.WithTags("ATS")
		.Produces<GetTicketedOrdersEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Ticketed Orders")
		.WithDescription(
			"Retrieves the caller's orders queued for OMS auto-ticketing with keyset "
			+ "pagination, optionally filtered by ticket status, including the OMS "
			+ "ticket number and delivery date once a ticket has been raised.")
		.RequireAuthorization();
	}
}
