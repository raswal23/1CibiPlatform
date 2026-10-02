namespace ATS.Features.Web.GetApplicationFormPreview;

public record GetApplicationFormPreviewEndpointRequest(Guid EmailInvitationRequestId);

public record GetApplicationFormPreviewEndpointResponse(ApplicationFormPreviewDTO Preview);

public class GetApplicationFormPreviewEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getapplicationformpreview", async (
			[AsParameters] GetApplicationFormPreviewEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetApplicationFormPreviewQueryRequest(request.EmailInvitationRequestId);
			var result = await sender.Send(query, cancellationToken);
			return Results.Ok(new GetApplicationFormPreviewEndpointResponse(result.Preview));
		})
		.WithName("GetApplicationFormPreview")
		.WithTags("ATS")
		.Produces<GetApplicationFormPreviewEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get Application Form Preview")
		.WithDescription("Retrieves the application form answers for one order, for the read-only preview dialog.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
