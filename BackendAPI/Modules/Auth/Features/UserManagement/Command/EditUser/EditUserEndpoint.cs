namespace Auth.Features.UserManagement.Command.EditUser;
public record EditUserRequest(EditUserDTO editUser);

public record EditUserResponse(UserDTO user);
public class EditUserEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("auth/edituser", async (EditUserRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditUserCommand(request.editUser);
			EditUserResult result = await sender.Send(command, cancellationToken);
			var response = new EditUserResponse(result.user);
			return Results.Ok(response.user);
		})
		.WithName("EditUser")
		.WithTags("User Management")
		.Produces<UserDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit User")
		.WithDescription("Edits an existing user in OnePlatform.")
		.RequireAuthorization();
	}
}
