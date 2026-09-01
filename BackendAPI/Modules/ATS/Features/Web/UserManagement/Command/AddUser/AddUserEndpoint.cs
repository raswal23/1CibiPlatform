namespace ATS.Features.Web.UserManagement.Command.AddUser;

public record AddUserRequest(IReadOnlyCollection<AddUserDTO> users);

public record AddUserResponse(bool isAdded);

public class AddUserEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("adduser", async (AddUserRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddUserCommand(request.users);
			var result = await sender.Send(command, cancellationToken);
			var response = new AddUserResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("ATSAddUser")
		.WithTags("ATS User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.WithSummary("Add ATS User")
		.WithDescription("Adds a logical ATS user and all selected module assignments.")
		.RequireAuthorization();
	}
}
