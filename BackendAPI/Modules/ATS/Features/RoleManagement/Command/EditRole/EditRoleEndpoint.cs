namespace ATS.Features.RoleManagement.Command.EditRole;

public record EditRoleRequest(EditRoleDTO editRole);

public record EditRoleResponse(RoleDetailsDTO role);

public class EditRoleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("editrole", async (EditRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditRoleCommand(request.editRole);
			EditRoleResult result = await sender.Send(command, cancellationToken);
			var response = new EditRoleResponse(result.role);
			return Results.Ok(response.role);
		})
		.WithName("ATSEditRole")
		.WithTags("Role Management")
		.Produces<RoleDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit Role")
		.WithDescription("Edits an existing ATS role.")
		.RequireAuthorization();
	}
}
