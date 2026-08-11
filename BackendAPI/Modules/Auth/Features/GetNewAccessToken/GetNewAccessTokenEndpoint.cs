namespace Auth.Features.GetNewAccessToken;

public record GetNewAccessTokenResponse(LoginResponseWebDTO LoginResponseWebDTO);


public class GetNewAccessTokenEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("getnewaccesstoken", async (ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new GetNewAccessTokenCommand();

			GetNewAccessTokenResult result = await sender.Send(command, cancellationToken);

			GetNewAccessTokenResponse getNewAccessTokenResponse = new GetNewAccessTokenResponse(result.loginResponseWebDTO);

			return Results.Ok(getNewAccessTokenResponse.LoginResponseWebDTO);
		})
		.WithName("GetNewAccessToken")
		.WithTags("Authentication")
		.Produces<LoginResponseWebDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get New Access Token")
		.WithDescription("Get New Access Token using Refresh Token");
	}
}
