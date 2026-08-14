namespace Auth.Features.UserManagement.Command.DeleteApplication;
public record DeleteApplicationRequest(int AppId);
public record DeleteApplicationResponse(bool IsDeleted);
public class DeleteApplicationEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapDelete("auth/deleteapplication/{AppId}", async (int AppId, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new DeleteApplicationCommand(AppId);
			DeleteApplicationResult result = await sender.Send(command, cancellationToken);
			var response = new DeleteApplicationResponse(result.IsDeleted);
			return Results.Ok(response.IsDeleted);
		})
		.WithName("DeleteApplication")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Delete Application")
		.WithDescription("Deletes an existing application in OnePlatform.")
		.RequireAuthorization();
	}
}
