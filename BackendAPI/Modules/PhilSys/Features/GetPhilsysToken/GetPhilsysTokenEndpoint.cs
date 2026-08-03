namespace PhilSys.Features.GetPhilSysToken;
public record GetPhilSysTokenRequest(string client_id, string client_secret);
public record GetPhilSysTokenResponse(string AccessToken);
public class GetPhilsysTokenEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("getphilsystoken", async (GetPhilSysTokenRequest request, ISender sender) =>
		{
			var command = new GetPhilSysTokenCommand(
				   request.client_id,
				   request.client_secret
			   );
			GetCredentialResult result = await sender.Send(command);
			var response = new GetPhilSysTokenResponse(result.AccessToken);
			return Results.Ok(response.AccessToken);
		})
		.WithName("GetPhilSysToken")
		.WithTags("PhilSys")
		.Produces<string>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Retrieve PhilSys Token")
		.WithDescription("Retrieves an access token from the PhilSys API using client credentials.");
	}
}
