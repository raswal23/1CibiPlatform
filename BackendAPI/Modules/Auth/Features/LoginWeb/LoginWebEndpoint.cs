using static Auth.Features.LoginWeb.LoginWebHandler;

namespace Auth.Features.LoginWeb;

public record LoginWebRequest(LoginWebCred loginWebCred);

public record LoginWebResponse(LoginResponseWebDTO loginResponseWebDTO);


public class LoginWebEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("loginweb", async (LoginWebRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new LoginWebCommand(request.loginWebCred);

			LoginWebResult result = await sender.Send(command, cancellationToken);

			LoginWebResponse loginResponse = new LoginWebResponse(result.loginResponseWebDTO);

			return Results.Ok(loginResponse.loginResponseWebDTO);
		})
		  .WithName("Loginweb")
		  .WithTags("Authentication")
		  .Produces<LoginResponseWebDTO>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Login")
		  .WithDescription("Login");
	}
}

