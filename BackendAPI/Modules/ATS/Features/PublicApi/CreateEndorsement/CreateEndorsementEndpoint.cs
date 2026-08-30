namespace ATS.Features.PublicApi.CreateEndorsement;

public record CreateEndorsementEndpointRequest(
	string FirstName,
	string LastName,
	string? MiddleInitial,
	string EmailAddress,
	string MobileNumber,
	string Package,
	string OrderType);

public record CreateEndorsementEndpointResponse(bool Success);

public class CreateEndorsementEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("api/public/ats/endorsements", async (
			CreateEndorsementEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new CreateEndorsementCommand(
				request.FirstName,
				request.LastName,
				request.MiddleInitial,
				request.EmailAddress,
				request.MobileNumber,
				request.Package,
				request.OrderType);

			var result = await sender.Send(command, cancellationToken);

			return Results.Ok(new CreateEndorsementEndpointResponse(result.Success).Success);
		})
		.WithName("PublicCreateEndorsement")
		.WithTags("ATS Public API")
		.Produces<bool>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.WithSummary("Create an endorsement")
		.WithDescription(
			"Creates a single background-check order and emails the application form to "
			+ "the subject. The order is attributed to the client the access token "
			+ "belongs to.")
		.RequireAuthorization();
	}
}
