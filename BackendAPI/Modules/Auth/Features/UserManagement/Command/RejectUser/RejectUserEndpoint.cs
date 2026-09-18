namespace Auth.Features.UserManagement.Command.RejectUser;
public record RejectUserRequest(Guid UserId);
public record RejectUserResponse(bool IsRejected);
public class RejectUserEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapDelete("auth/rejectuser/{UserId}", async (Guid UserId, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new RejectUserCommand(UserId);
			RejectUserResult result = await sender.Send(command, cancellationToken);
			var response = new RejectUserResponse(result.IsRejected);
			return Results.Ok(response.IsRejected);
		})
		.WithName("RejectUser")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Reject User")
		.WithDescription("Rejects a user awaiting approval, deactivating the account so it leaves the approval queue.")
		.RequireAuthorization();
	}
}
