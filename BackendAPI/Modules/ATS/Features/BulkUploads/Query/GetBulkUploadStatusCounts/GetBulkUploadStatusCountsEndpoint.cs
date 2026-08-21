namespace ATS.Features.BulkUploads.Query.GetBulkUploadStatusCounts;

public record GetBulkUploadStatusCountsEndpointRequest(
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetBulkUploadStatusCountsEndpointResponse(BulkUploadStatusCountsDTO Counts);

public class GetBulkUploadStatusCountsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getbulkuploadstatuscounts", async (
			[AsParameters] GetBulkUploadStatusCountsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetBulkUploadStatusCountsQueryRequest(
				request.SearchTerm,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetBulkUploadStatusCountsEndpointResponse(result.Counts));
		})
		.WithName("GetBulkUploadStatusCounts")
		.WithTags("ATS")
		.Produces<GetBulkUploadStatusCountsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Bulk Upload Status Counts")
		.WithDescription(
			"Returns how many of the caller's bulk upload files are Pending, Processing "
			+ "and Done. Honours the search and date filters but never the selected status, "
			+ "so every bucket keeps reporting its own size.")
		.RequireAuthorization();
	}
}
