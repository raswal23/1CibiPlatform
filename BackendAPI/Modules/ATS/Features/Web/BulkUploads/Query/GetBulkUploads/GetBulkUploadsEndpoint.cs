namespace ATS.Features.Web.BulkUploads.Query.GetBulkUploads;

public record GetBulkUploadsEndpointRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? Status = null,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);

public record GetBulkUploadsEndpointResponse(KeysetPaginatedResult<BulkUploadListDTO> BulkUploads);

public class GetBulkUploadsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getbulkuploads", async (
			[AsParameters] GetBulkUploadsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetBulkUploadsQueryRequest(
				request.Cursor,
				request.PageSize,
				request.Status,
				request.SearchTerm,
				request.StartDate,
				request.EndDate);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetBulkUploadsEndpointResponse(result.BulkUploads));
		})
		.WithName("GetBulkUploads")
		.WithTags("ATS")
		.Produces<GetBulkUploadsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Bulk Uploads")
		.WithDescription(
			"Retrieves the caller's bulk upload files with keyset pagination, optionally "
			+ "filtered by status, including the subject count and email-send progress of "
			+ "the invitations each file produced.")
		.RequireAuthorization();
	}
}
