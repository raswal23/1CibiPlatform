namespace Auth.Features.UserManagement.Command.EditSubMenu;

public record EditSubMenuRequest(EditSubMenuDTO editSubMenu);

public record EditSubMenuResponse(SubMenuDTO subMenu);
public class EditSubMenuEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("auth/editsubmenu", async (EditSubMenuRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditSubMenuCommand(request.editSubMenu);
			EditSubMenuResult result = await sender.Send(command, cancellationToken);
			var response = new EditSubMenuResponse(result.subMenu);
			return Results.Ok(response.subMenu);
		})
		.WithName("EditSubMenu")
		.WithTags("User Management")
		.Produces<SubMenuDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit SubMenu")
		.WithDescription("Edits an existing submnu in OnePlatform.")
		.RequireAuthorization();
	}
}
