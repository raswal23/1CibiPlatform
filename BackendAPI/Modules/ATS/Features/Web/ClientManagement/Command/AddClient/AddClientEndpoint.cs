namespace ATS.Features.Web.ClientManagement.Command.AddClient;

public record AddClientRequest(IReadOnlyCollection<AddClientDTO> clients);

public record AddClientResponse(bool isAdded);

public class AddClientEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("addclient", async (AddClientRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddClientCommand(request.clients);
			AddClientResult result = await sender.Send(command, cancellationToken);
			var response = new AddClientResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("AddClient")
		.WithTags("Client Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add Client")
		.WithDescription("Add a new client.")
		.RequireAuthorization();
	}
}
