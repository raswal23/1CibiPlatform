namespace Auth.Features.UserManagement.Command.DeleteSubMenu;

public record DeleteSubMenuRequest(int SubMenuId);
public record DeleteSubMenuResponse(bool IsDeleted);
public class DeleteSubMenuEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapDelete("auth/deletesubmenu/{SubMenuId}", async (int SubMenuId, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new DeleteSubMenuCommand(SubMenuId);
			DeleteSubMenuResult result = await sender.Send(command, cancellationToken);
			var response = new DeleteSubMenuResponse(result.IsDeleted);
			return Results.Ok(response.IsDeleted);
		})
		.WithName("DeleteSubMenu")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Delete SubMenu")
		.WithDescription("Deletes an existing submenu in OnePlatform.")
		.RequireAuthorization();
	}
}
