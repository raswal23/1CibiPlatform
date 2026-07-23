namespace ATS.Features.DownloadMultipleOrderRecords;

public record DownloadMultipleOrderRecordsRequest(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest);


public class DownloadMultipleOrderRecordsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("downloadmultipleorderrecords", async (DownloadMultipleOrderRecordsRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new DownloadMultipleOrderRecordsHandlerRequest(request.downloadMultipleOrderRecordsRequest);
			Stream zipStream = await sender.Send(command, cancellationToken);
			return Results.File(
				zipStream,
				"application/zip",
				"ATS_Order_Records.zip");
		})
		.WithName("DownloadMultipleOrderRecords")
		.WithTags("ATS")
		.Produces(StatusCodes.Status200OK, contentType: "application/zip")
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Download Multiple Order Records")
		.WithDescription("Downloads the selected order records as a ZIP archive.")
		.RequireAuthorization(); ;
	}
}
