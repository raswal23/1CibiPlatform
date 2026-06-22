namespace PhilSys.Features.InternalSystem;

public record InternalSystemRequest(string callback_url, string inquiry_type, IdentityData identity_data);
public record InternalSystemResponse(PartnerSystemResponseDTO PartnerSystemResponseDTO);
public class InternalSystemEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("internalsystemquery", async (InternalSystemRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new InternalSystemCommand(request.callback_url, request.inquiry_type, request.identity_data);
			InternalSystemResult result = await sender.Send(command, cancellationToken);
			var response = new InternalSystemResponse(result.PartnerSystemResponseDTO);
			return Results.Json(response.PartnerSystemResponseDTO);
		})
		  .WithName("InternalSystemQuery")
		  .WithTags("PhilSys")
		  .Produces<InternalSystemResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Initiate Internal System Identity Verification")
		  .WithDescription("Initializes a new PhilSys Internal System identity verification transaction and generates a unique liveness verification link.");
	}
}
