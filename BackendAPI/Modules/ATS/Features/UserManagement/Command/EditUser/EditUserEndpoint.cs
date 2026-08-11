namespace ATS.Features.UserManagement.Command.EditUser;

public record EditUserRequest(IReadOnlyCollection<EditUserDTO> editUsers);

public record EditUserResponse(IReadOnlyList<UserDetailsDTO> users);

public class EditUserEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("edituser", async (EditUserRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditUserCommand(request.editUsers);
			var result = await sender.Send(command, cancellationToken);
			var response = new EditUserResponse(result.users);
			return Results.Ok(response.users);
		})
		.WithName("ATSEditUser")
		.WithTags("ATS User Management")
		.Produces<IReadOnlyList<UserDetailsDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Edit ATS User")
		.WithDescription("Edits an ATS user and synchronizes the selected module assignments.")
		.RequireAuthorization();
	}
}
