namespace PhilSys.Features.PartnerSystemQuery;
public record PartnerSystemRequest(string callback_url, string inquiry_type, IdentityData identity_data);
public record PartnerSystemResponse(PartnerSystemResponseDTO PartnerSystemResponseDTO);
public class PartnerSystemEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("partnersystemquery", async (PartnerSystemRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new PartnerSystemCommand(request.callback_url, request.inquiry_type, request.identity_data);
			PartnerSystemResult result = await sender.Send(command, cancellationToken);
			var response = new PartnerSystemResponse(result.PartnerSystemResponseDTO);
			return Results.Json(response.PartnerSystemResponseDTO);
		})
		  .WithName("PartnerSystemQuery")
		  .WithTags("PhilSys")
		  .Produces<PartnerSystemResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Initiate Partner System Identity Verification")
		  .WithDescription("Initializes a new PhilSys Partner System identity verification transaction and generates a unique liveness verification link.")
		  .RequireAuthorization();
	}
}
