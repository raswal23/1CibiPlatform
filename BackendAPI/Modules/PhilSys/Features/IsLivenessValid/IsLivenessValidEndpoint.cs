namespace PhilSys.Features.IsLivenessValid;
public record IsLivenessValidRequest(string HashToken);
public record IsLivenessValidResponse(TransactionStatusResponseDTO TransactionStatusResponseDTO);
public class IsLivenessValidEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("idv/validate/liveness", async (IsLivenessValidRequest request, ISender sender) =>
		{
			var command = new IsLivenessValidCommand(
				request.HashToken
				);
			IsLivenessValidResult result = await sender.Send(command);
			var response = new IsLivenessValidResponse(result.TransactionStatusResponseDTO);
			return Results.Ok(response.TransactionStatusResponseDTO);
		})
		.WithName("isLivenessVerified")
		.WithTags("PhilSys")
		.Produces<TransactionStatusResponseDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Liveness Session")
		.WithDescription("Validates whether a PhilSys liveness session associated with a given hash token has not expired or been transacted.");
	}
}
