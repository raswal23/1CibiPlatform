namespace ATS.Features.Web.OMSTicketing.Command.RetryTickets;

public record RetryTicketsEndpointRequest(IReadOnlyCollection<Guid> EmailInvitationIds);

public record RetryTicketsEndpointResponse(int RequestedCount, int RequeuedCount, bool IsComplete);

public class RetryTicketsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("retrytickets", async (
			RetryTicketsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new RetryTicketsCommand(request.EmailInvitationIds ?? []);

			var result = await sender.Send(command, cancellationToken);

			var response = new RetryTicketsEndpointResponse(
				result.RequestedCount,
				result.RequeuedCount,
				result.IsComplete);

			// The full result, not a bool: a selection made a minute ago can contain orders
			// the job has since picked up, so "3 of 5" is a normal outcome the operator has
			// to be able to see.
			return Results.Ok(response);
		})
		.WithName("RetryTickets")
		.WithTags("ATS")
		.Produces<RetryTicketsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Retry OMS Ticketing for many orders")
		.WithDescription(
			"Puts every selected order whose automatic OMS ticketing retries are exhausted "
			+ "back on the queue with a fresh attempt budget. Orders outside the caller's "
			+ "scope are ignored, and orders that are no longer exhausted are skipped, so "
			+ "the response reports how many of the requested orders actually moved. "
			+ "Returns 400 above the batch limit and 404 when nothing in the selection is "
			+ "available to the caller.")
		.RequireAuthorization();
	}
}
