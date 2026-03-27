namespace Auth.Features.UpdatePassword;

public record UpdatePasswordRequest(UpdatePasswordRequestDTO UpdatePasswordRequestDTO);

public record UpdatePasswordResponse(bool IsSuccessful);

public class UpdatePasswordEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("change-password",
			async (UpdatePasswordRequest request,
				  ISender sender,
				  CancellationToken cancellationToken) =>
			{
				var command = new UpdatePasswordCommand(request.UpdatePasswordRequestDTO);

				UpdatePasswordResult resultFromHandler = await sender.Send(command, cancellationToken);

				var result = new UpdatePasswordResponse(resultFromHandler.IsSuccessful);

				return Results.Ok(result);
			})
			.WithName("UpdatePassword")
			.WithTags("Authentication")
			.Produces<UpdatePasswordResponse>(StatusCodes.Status200OK)
			.WithSummary("Updates the user's password.")
			.WithDescription("Updates the user's password using the provided Hash and new password.");
	}
}
