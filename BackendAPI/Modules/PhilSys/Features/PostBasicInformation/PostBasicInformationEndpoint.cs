namespace PhilSys.Features.PostBasicInformation;
public record PostBasicInformationRequest(string first_name,
										  string middle_name,
										  string last_name,
										  string suffix,
										  string birth_date,
										  string bearer_token,
										  string face_liveness_session_id);
public record PostBasicInformationResponse(BasicInformationOrPCNResponseDTO BasicInformationResponseDTO);
public class PostBasicInformationEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("postbasicinformation", async (PostBasicInformationRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new PostBasicInformationCommand(
				request.first_name,
				request.middle_name,
				request.last_name,
				request.suffix,
				request.birth_date,
				request.bearer_token,
				request.face_liveness_session_id
				);
			PostBasicInformationResult result = await sender.Send(command, cancellationToken);
			var response = new PostBasicInformationResponse(result.BasicInformationResponseDTO);
			return Results.Ok(response.BasicInformationResponseDTO);
		})
		.WithName("PostBasicInformation")
		.WithTags("PhilSys")
		.Produces<BasicInformationOrPCNResponseDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Submit Basic Information For Identity Verification")
		.WithDescription("Sends a request to the PhilSys API to submit basic demographic information for identity verification.");
	}
}
