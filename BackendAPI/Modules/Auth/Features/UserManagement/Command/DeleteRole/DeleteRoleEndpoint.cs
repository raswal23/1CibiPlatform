namespace Auth.Features.UserManagement.Command.DeleteRole;
public record DeleteRoleRequest(int RoleId);
public record DeleteRoleResponse(bool IsDeleted);
public class DeleteRoleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapDelete("auth/deleterole/{RoleId}", async (int RoleId, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new DeleteRoleCommand(RoleId);
			DeleteRoleResult result = await sender.Send(command, cancellationToken);
			var response = new DeleteRoleResponse(result.IsDeleted);
			return Results.Ok(response.IsDeleted);
		})
		.WithName("DeleteRole")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Delete Role")
		.WithDescription("Deletes an existing role in OnePlatform.")
		.RequireAuthorization();
	}
}



	
