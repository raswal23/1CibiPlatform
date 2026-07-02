namespace Auth.Features.UserManagement.Command.AddAppSubRole;
public record AddAppSubRoleRequest(AddAppSubRoleDTO appSubRole);
public record AddAppSubRoleResponse(bool isAdded);
public class AddAppSubRoleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("auth/addappsubrole", async (AddAppSubRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddAppSubRoleCommand(
				request.appSubRole
				);
			AddAppSubRoleResult result = await sender.Send(command, cancellationToken);
			var response = new AddAppSubRoleResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("AddAppSubRole")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add AppSubRole")
		.WithDescription("Add an application, submenu, and a role for a user in OnePlatform")
		.RequireAuthorization();
	}
}


