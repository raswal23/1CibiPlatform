namespace ATS.Features.OMSTicketing.Command.RetryTicket;

public record RetryTicketEndpointRequest(Guid EmailInvitationId);

public record RetryTicketEndpointResponse(bool Success);

public class RetryTicketEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("retryticket", async (
			RetryTicketEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new RetryTicketCommand(request.EmailInvitationId);

			var result = await sender.Send(command, cancellationToken);

			var response = new RetryTicketEndpointResponse(result.Success);

			return Results.Ok(response.Success);
		})
		.WithName("RetryTicket")
		.WithTags("ATS")
		.Produces<bool>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.ProducesProblem(StatusCodes.Status409Conflict)
		.WithSummary("Retry OMS Ticketing")
		.WithDescription(
			"Puts an order whose automatic OMS ticketing retries are exhausted back on "
			+ "the queue with a fresh attempt budget. Returns 409 when the order is no "
			+ "longer awaiting a retry, and 404 when it is unknown or outside the "
			+ "caller's scope.")
		.RequireAuthorization();
	}
}
