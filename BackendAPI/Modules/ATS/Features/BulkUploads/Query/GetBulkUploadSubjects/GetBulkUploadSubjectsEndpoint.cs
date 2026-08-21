namespace ATS.Features.BulkUploads.Query.GetBulkUploadSubjects;

// FileId travels as a query parameter, not a route template: ATSPaths MatchPath entries
// are literal strings, so a {fileId} segment would need a catch-all route plus a
// PathPattern transform. getpackages threads its ClientId the same way.
public record GetBulkUploadSubjectsEndpointRequest(
	Guid FileId,
	string? Cursor = null,
	int? PageSize = 10,
	string? EmailStatus = null,
	string? SearchTerm = null);

public record GetBulkUploadSubjectsEndpointResponse(BulkUploadSubjectsResultDTO Result);

public class GetBulkUploadSubjectsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getbulkuploadsubjects", async (
			[AsParameters] GetBulkUploadSubjectsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetBulkUploadSubjectsQueryRequest(
				request.FileId,
				request.Cursor,
				request.PageSize,
				request.EmailStatus,
				request.SearchTerm);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetBulkUploadSubjectsEndpointResponse(result.Result));
		})
		.WithName("GetBulkUploadSubjects")
		.WithTags("ATS")
		.Produces<GetBulkUploadSubjectsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get Bulk Upload Subjects")
		.WithDescription(
			"Retrieves the subjects a single bulk upload file produced, with keyset "
			+ "pagination and an optional email-status filter. Returns 404 when the file "
			+ "is unknown or outside the caller's client/requestor scope.")
		.RequireAuthorization();
	}
}
