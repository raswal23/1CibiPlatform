namespace Auth.Features.UserManagement.Command.EditApplication;

public record EditApplicationRequest(EditApplicationDTO editApplication);

public record EditApplicationResponse(ApplicationDTO application);
public class EditApplicationEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("auth/editapplication", async (EditApplicationCommand request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditApplicationCommand(request.editApplication);
			EditApplicationResult result = await sender.Send(command, cancellationToken);
			var response = new EditApplicationResponse(result.application);
			return Results.Ok(response.application);
		})
		.WithName("EditApplication")
		.WithTags("User Management")
		.Produces<ApplicationDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit Application")
		.WithDescription("Edits an existing application in OnePlatform.")
		.RequireAuthorization();
	}
}
