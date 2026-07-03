namespace Auth.Features.UserManagement.Command.DeleteLockedUser;

public record DeleteLockedUserRequest(Guid lockUserId);
public record DeleteLockedUserResponse(bool IsDeleted);

public class DeleteLockedUserEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapDelete("auth/deletelockeduser/{lockUserId}", async (Guid lockUserId, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new DeleteLockedUserCommand(lockUserId);
			DeleteLockedUserResult result = await sender.Send(command, cancellationToken);
			var response = new DeleteLockedUserResponse(result.IsDeleted);
			return Results.Ok(response.IsDeleted);
		})
		.WithName("DeleteLockedUser")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Delete Locked User")
		.WithDescription("Deletes an existing locked user in OnePlatform.")
		.RequireAuthorization();
	}
}

