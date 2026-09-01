namespace ATS.Features.PublicApi.DownloadReport;

public record DownloadReportEndpointRequest(List<string> DocumentTypes);

public class DownloadReportEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("api/public/ats/orders/{orderId:guid}/report", async (
			Guid orderId,
			DownloadReportEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new DownloadReportCommand(orderId, request.DocumentTypes);

			var result = await sender.Send(command, cancellationToken);

			// The archive name is derived server-side from the order the caller was
			// actually allowed to read, never from anything they supplied.
			return Results.File(
				result.ZipStream,
				"application/zip",
				$"{result.SubjectName}.zip");
		})
		.WithName("PublicDownloadReport")
		.WithTags("ATS Public API")
		.Produces(StatusCodes.Status200OK, contentType: "application/zip")
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Download an order's documents")
		.WithDescription(
			"Returns the requested documents for a completed order as a ZIP archive. "
			+ "Returns 404 when the order does not belong to the access token's client.")
		.RequireAuthorization();
	}
}
