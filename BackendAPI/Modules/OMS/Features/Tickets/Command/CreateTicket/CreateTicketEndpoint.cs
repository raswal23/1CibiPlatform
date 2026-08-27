namespace OMS.Features.Tickets.Command.CreateTicket;

public sealed class CreateTicketEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost(
				"api/oms/tickets",
				async (
					CreateOMSTicketRequest request,
					ISender sender,
					CancellationToken cancellationToken) =>
				{
					var command = new CreateTicketCommand(request);
					var result = await sender.Send(command, cancellationToken);

					return Results.Ok(result);
				})
			.RequireAuthorization()
			.WithName("OMSCreateTicket")
			.WithTags("OMS")
			.Produces<OMSTicketCreated>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status500InternalServerError)
			.WithSummary("Create an OMS ticket")
			.WithDescription(
				"Validates the requestor and PO entitlement against the legacy OMS "
				+ "database, then creates a ticket via stored procedure and returns "
				+ "the generated ticket number and delivery date.");
	}
}
