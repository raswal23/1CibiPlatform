namespace Auth.Features.UserManagement.Command.EditRole;

public record EditRoleRequest(EditRoleDTO editRole);

public record EditRoleResponse(RoleDTO role);
public class EditRoleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("auth/editrole", async (EditRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditRoleCommand(request.editRole);
			EditRoleResult result = await sender.Send(command, cancellationToken);
			var response = new EditRoleResponse(result.role);
			return Results.Ok(response.role);
		})
		.WithName("EditRole")
		.WithTags("User Management")
		.Produces<RoleDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit Role")
		.WithDescription("Edits an existing role in OnePlatform.")
		.RequireAuthorization();
	}
}


	
