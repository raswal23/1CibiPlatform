namespace ATS.Features.PublicApi.GetBulkUploadStatus;

public record GetBulkUploadStatusEndpointResponse(PublicBulkUploadStatusDTO Upload);

public class GetBulkUploadStatusEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("api/public/ats/endorsements/bulk/{fileId:guid}", async (
			Guid fileId,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetBulkUploadStatusQueryRequest(fileId);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetBulkUploadStatusEndpointResponse(result.Upload));
		})
		.WithName("PublicGetBulkUploadStatus")
		.WithTags("ATS Public API")
		.Produces<GetBulkUploadStatusEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get a bulk upload's result")
		.WithDescription(
			"Returns how a CSV upload was parsed: how many rows became orders, and which "
			+ "rows were skipped with the reason for each. Status is Pending until the "
			+ "file has been picked up, then Processing, then Done.")
		.RequireAuthorization();
	}
}
