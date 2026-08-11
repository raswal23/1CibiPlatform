namespace ATS.Features.RoleManagement.Command.AddRole;

public record AddRoleRequest(AddRoleDTO role);

public record AddRoleResponse(bool isAdded);

public class AddRoleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("addrole", async (AddRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddRoleCommand(request.role);
			AddRoleResult result = await sender.Send(command, cancellationToken);
			var response = new AddRoleResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("ATSAddRole")
		.WithTags("Role Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add Role")
		.WithDescription("Add a new ATS role.")
		.RequireAuthorization();
	}
}
