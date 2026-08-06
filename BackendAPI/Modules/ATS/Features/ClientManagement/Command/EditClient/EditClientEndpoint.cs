namespace ATS.Features.ClientManagement.Command.EditClient;

public record EditClientRequest(EditClientDTO editClient);

public record EditClientResponse(ClientDetailsDTO client);

public class EditClientEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("editclient", async (EditClientRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditClientCommand(request.editClient);
			EditClientResult result = await sender.Send(command, cancellationToken);
			var response = new EditClientResponse(result.client);
			return Results.Ok(response.client);
		})
		.WithName("EditClient")
		.WithTags("Client Management")
		.Produces<ClientDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit Client")
		.WithDescription("Edits an existing client.")
		.RequireAuthorization();
	}
}
