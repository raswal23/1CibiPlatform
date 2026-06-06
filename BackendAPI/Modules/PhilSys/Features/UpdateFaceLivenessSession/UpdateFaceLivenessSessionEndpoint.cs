namespace PhilSys.Features.PostFaceLivenessSession;
public record UpdateFaceLivenessSessionRequest(string HashToken, string FaceLivenessSessionId);
public record UpdateFaceLivenessSessionResponse(VerificationResponseDTO VerificationResponseDTO);
public class UpdateFaceLivenessSessionEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("updatefacelivenesssession", async (UpdateFaceLivenessSessionRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new UpdateFaceLivenessSessionCommand(
				request.HashToken,
				request.FaceLivenessSessionId
				);
			UpdateFaceLivenessSessionResult result = await sender.Send(command, cancellationToken);

			var response = new UpdateFaceLivenessSessionResponse(result.VerificationResponseDTO);

			return Results.Ok(response.VerificationResponseDTO);
		})
		.WithName("UpdateFaceLivenessSession")
		.WithTags("PhilSys")
		.Produces<UpdateFaceLivenessSessionResponse>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Update Face Liveness Session and Process Verification")
		.WithDescription("Updates the Face Liveness Session ID for an existing PhilSys transaction and triggers verification based on the inquiry type.");
	}
}
