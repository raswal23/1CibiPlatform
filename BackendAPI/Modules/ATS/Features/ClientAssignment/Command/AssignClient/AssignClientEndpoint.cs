namespace ATS.Features.ClientAssignment.Command.AssignClient;

public record AssignClientRequest(AssignUserClientDTO Assignment);

public sealed class AssignClientEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPut("assignclient", async (
			AssignClientRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(
				new AssignClientCommand(request.Assignment),
				cancellationToken);
			return Results.Ok(result.Assignment);
		})
		.WithName("ATSAssignClient")
		.WithTags("ATS Client Assigning")
		.Produces<ClientAssignmentDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Assign or replace an ATS user's client")
		.WithDescription("Creates the user's first assignment or replaces the existing client. Re-selecting the current client is a no-op.")
		.RequireAuthorization();
	}
}
