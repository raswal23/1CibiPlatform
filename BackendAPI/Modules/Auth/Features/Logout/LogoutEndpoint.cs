namespace Auth.Features.Logout;

public record LogoutResponse(bool IsLoggedOut);

public class LogoutEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("logout", async (ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new LogoutCommand();

			LogoutResult result = await sender.Send(command, cancellationToken);

			LogoutResponse response = new LogoutResponse(result.IsLoggedOut);

			return Results.Ok(response);

		})
		.WithName("Logout")
		.WithTags("Authentication")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Logout")
		.WithDescription("Logout");
	}
}
