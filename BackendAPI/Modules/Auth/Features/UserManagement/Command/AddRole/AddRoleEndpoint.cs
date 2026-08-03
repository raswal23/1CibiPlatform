namespace Auth.Features.UserManagement.Command.AddRole;
public record AddRoleRequest(AddRoleDTO role);
public record AddRoleResponse(bool isAdded);
public class AddRoleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("auth/addrole", async (AddRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddRoleCommand(
				request.role
				);
			AddRoleResult result = await sender.Send(command, cancellationToken);
			var response = new AddRoleResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("AddRole")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add Role")
		.WithDescription("Add a role in OnePlatform.")
		.RequireAuthorization();
	}
}



