namespace ATS.Features.Report;

public record GetReportResultEndpointRequest(Guid EmailInvitationRequestId);

public record GetReportResultEndpointResponse(ReportResultDTO ReportResult);

public class GetReportResultEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getreportresult", async (
			[AsParameters] GetReportResultEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetReportResultQueryRequest(request.EmailInvitationRequestId);
			var result = await sender.Send(query, cancellationToken);
			return Results.Ok(new GetReportResultEndpointResponse(result.ReportResult));
		})
		.WithName("GetReportResult")
		.WithTags("ATS")
		.Produces<GetReportResultEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get Report Result")
		.WithDescription("Retrieves ATS result details for a specific email invitation request.")
		.RequireAuthorization();
	}
}
