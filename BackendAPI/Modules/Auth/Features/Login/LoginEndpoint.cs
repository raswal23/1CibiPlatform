namespace Auth.Features.Login;

public record LoginRequest(string username, string password);

public record LoginResponse(LoginResponseDTO LoginResponseDTO);


public class LoginEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
		{

			var command = new LoginCommand(request.username, request.password);

			LoginResult result = await sender.Send(command, cancellationToken);

			LoginResponse loginResponse = new LoginResponse(result.loginResponseDTO);

			return Results.Ok(loginResponse.LoginResponseDTO);
		})
		.WithName("Login")
		.WithTags("Authentication")
		.Produces<LoginResponseDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Login")
		.WithDescription("Login User");
	}
}
