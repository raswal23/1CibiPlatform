namespace ATS.Features.PublicApi.CreateEndorsement;

public record CreateEndorsementEndpointRequest(
	string FirstName,
	string LastName,
	string? MiddleInitial,
	string EmailAddress,
	string MobileNumber,
	string Package,
	string OrderType);

/// <summary>
/// A bare `true` told a caller nothing they could act on. This names the outcome and
/// hands back the order id, so an integrator can record it and poll the order without
/// a second call to find what they just created.
/// </summary>
public record CreateEndorsementEndpointResponse(
	bool IsSuccessful,
	Guid OrderId,
	string Package,
	string OrderType,
	string Message);

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

			return Results.Ok(new CreateEndorsementEndpointResponse(
				result.IsSuccessful,
				result.OrderId,
				result.Package,
				result.OrderType,
				"The order was created and the application form has been emailed to the subject."));
		})
		.WithName("PublicCreateEndorsement")
		.WithTags("ATS Public API")
		.Produces<CreateEndorsementEndpointResponse>(StatusCodes.Status200OK)
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
