namespace Auth.Features.UserManagement.Command.AddApplication;

public record AddApplicationRequest(AddApplicationDTO application);
public record AddApplicationResponse(bool isAdded);
public class AddApplicationEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("auth/addapplication", async (AddApplicationRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddApplicationCommand(
				request.application
				);
			AddApplicationResult result = await sender.Send(command, cancellationToken);
			var response = new AddApplicationResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("AddApplication")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add Application")
		.WithDescription("Add an application in OnePlatform.")
		.RequireAuthorization();
	}
}
