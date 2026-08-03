namespace Auth.Features.UserManagement.Command.EditAppSubRole;
public record EditAppSubRoleRequest(EditAppSubRoleDTO editAppSubRole);

public record EditAppSubRoleResponse(AppSubRoleDTO appSubRole);
public class EditAppSubRoleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("auth/editappsubrole", async (EditAppSubRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditAppSubRoleCommand(request.editAppSubRole);
			EditAppSubRoleResult result = await sender.Send(command, cancellationToken);
			var response = new EditAppSubRoleResponse(result.appSubRole);
			return Results.Ok(response.appSubRole);
		})
		.WithName("EditAppSubRole")
		.WithTags("User Management")
		.Produces<AppSubRoleDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit AppSubRole")
		.WithDescription("Edits an application, submenu, and a role of a user in OnePlatform")
		.RequireAuthorization();
	}
}


