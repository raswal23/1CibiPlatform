namespace ATS.Features.ClientManagement.Command.EditClient;

public record EditClientRequest(IReadOnlyCollection<EditClientDTO> editClients);

public record EditClientResponse(IReadOnlyList<ClientDetailsDTO> clients);

public class EditClientEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("editclient", async (EditClientRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditClientCommand(request.editClients);
			EditClientResult result = await sender.Send(command, cancellationToken);
			var response = new EditClientResponse(result.clients);
			return Results.Ok(response.clients);
		})
		.WithName("EditClient")
		.WithTags("Client Management")
		.Produces<IReadOnlyList<ClientDetailsDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit Client")
		.WithDescription("Edits an existing client.")
		.RequireAuthorization();
	}
}
