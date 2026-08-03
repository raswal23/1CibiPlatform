namespace PhilSys.Features.GetLivenessKey;

public record GetLivenessKeyRequest();
public record GetLivenessKeyResponse(string LivenessKey);
public class GetLivenessKeyEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("philsys/getlivenesskey", (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var request = new GetLivenessKeyQueryRequest();
			var result = sender.Send(request, cancellationToken);
			var response = result.Result.Adapt<GetLivenessKeyResponse>();
			return Results.Ok(response.LivenessKey);
		})
		.WithName("GetLivenessKey")
		.WithTags("PhilSys")
		.Produces<string>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Liveness Key")
		.WithDescription("Get Liveness Key for PhilSys verification");
	}
}
