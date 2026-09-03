namespace ATS.Features.Web.BulkUploads.Query.GetBulkUploadSubjectCounts;

public record GetBulkUploadSubjectCountsEndpointRequest(
	Guid FileId,
	string? SearchTerm = null);

public record GetBulkUploadSubjectCountsEndpointResponse(BulkUploadSubjectCountsDTO Counts);

public class GetBulkUploadSubjectCountsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getbulkuploadsubjectcounts", async (
			[AsParameters] GetBulkUploadSubjectCountsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetBulkUploadSubjectCountsQueryRequest(
				request.FileId,
				request.SearchTerm);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetBulkUploadSubjectCountsEndpointResponse(result.Counts));
		})
		.WithName("GetBulkUploadSubjectCounts")
		.WithTags("ATS")
		.Produces<GetBulkUploadSubjectCountsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get Bulk Upload Subject Counts")
		.WithDescription(
			"Returns how many of a bulk upload file's subjects are still Pending, Sent "
			+ "or Failed. Honours the search term but never the selected status, so every "
			+ "bucket keeps reporting its own size.")
		.RequireAuthorization();
	}
}
