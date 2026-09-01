namespace ATS.Features.Web.UserManagement.Command.AssignUserClient;

public record AssignUserClientRequest(AssignUserClientDTO assignment);

public record AssignUserClientResponse(UserClientDetailsDTO assignment);

public class AssignUserClientEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("assignuserclient", async (
			AssignUserClientRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(
				new AssignUserClientCommand(request.assignment),
				cancellationToken);
			return Results.Ok(new AssignUserClientResponse(result.assignment).assignment);
		})
		.WithName("ATSAssignUserClient")
		.WithTags("ATS User Management")
		.Produces<UserClientDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.WithSummary("Assign an ATS user to a client")
		.WithDescription("Creates or updates the client assignment and synchronizes existing access rows.")
		.RequireAuthorization();
	}
}
