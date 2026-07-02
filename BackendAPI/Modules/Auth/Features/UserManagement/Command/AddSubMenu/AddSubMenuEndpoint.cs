namespace Auth.Features.UserManagement.Command.AddSubMenu;

public record AddSubMenuRequest(AddSubMenuDTO subMenu);
public record AddSubMenuResponse(bool isAdded);
public class AddSubMenuEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("auth/addsubmenu", async (AddSubMenuRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddSubMenuCommand(
				request.subMenu
				);
			AddSubMenuResult result = await sender.Send(command, cancellationToken);
			var response = new AddSubMenuResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("AddSubMenu")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add SubMenu")
		.WithDescription("Add an submenu in OnePlatform.")
		.RequireAuthorization();
	}
}
