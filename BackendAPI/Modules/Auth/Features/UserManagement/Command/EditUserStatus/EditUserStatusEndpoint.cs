namespace Auth.Features.UserManagement.Command.EditUserStatus;
public record EditUserStatusRequest(EditUserStatusDTO editUserStatus);

public record EditUserStatusResponse(UserDTO user);
public class EditUserStatusEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("auth/edituserstatus", async (EditUserStatusRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditUserStatusCommand(request.editUserStatus);
			EditUserStatusResult result = await sender.Send(command, cancellationToken);
			var response = new EditUserStatusResponse(result.user);
			return Results.Ok(response.user);
		})
		.WithName("EditUserStatus")
		.WithTags("User Management")
		.Produces<UserDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Edit User Status")
		.WithDescription("Activates or deactivates an existing user in OnePlatform.")
		.RequireAuthorization();
	}
}
