namespace ATS.Features.Report;

public record GetReportsEndpointRequest(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null, string? SortColumn = null, bool SortDescending = false, DateTime? StartDate = null, DateTime? EndDate = null);

public record GetReportsEndpointResponse(PaginatedResult<ReportListDTO> Reports);

public class GetReportsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getreports", async (
			[AsParameters] GetReportsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetReportsQueryRequest(
				request.PageNumber,
				request.PageSize,
				request.SearchTerm,
				request.SortColumn,
				request.SortDescending,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetReportsEndpointResponse(result.Reports));
		})
		.WithName("GetReports")
		.WithTags("ATS")
		.Produces<GetReportsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Reports")
		.WithDescription("Retrieves paginated report results with latest hit status per invitation request.")
		.RequireAuthorization();
	}
}
