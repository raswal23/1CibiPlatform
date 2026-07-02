namespace PhilSys.Features.PostPCN;
public record PostPCNRequest(string value,
							 string bearer_token,
							 string face_liveness_session_id);
public record PostPCNResponse(BasicInformationOrPCNResponseDTO PCNResponseDTO);
public class PostPCNEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("postpcn", async (PostPCNRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new PostPCNCommand(
				request.value,
				request.bearer_token,
				request.face_liveness_session_id
				);

			PostPCNResult result = await sender.Send(command, cancellationToken);
			var response = new PostPCNResponse(result.PCNResponseDTO);
			return Results.Ok(response.PCNResponseDTO);
		})
		.WithName("PostPCN")
		.WithTags("PhilSys")
		.Produces<BasicInformationOrPCNResponseDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Submit PCN For Identity Verification")
		.WithDescription("Sends a request to the PhilSys API to submit Philsys Card Number for identity verification.");
	}
}
