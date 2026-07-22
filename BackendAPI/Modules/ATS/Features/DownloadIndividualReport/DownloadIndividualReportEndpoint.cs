namespace ATS.Features.DownloadIndividualReport;

public record DownloadIndividualReportRequest(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest);

public class DownloadIndividualReportEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("downloadindividualreport", async(DownloadIndividualReportRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new DownloadIndividualReportHandlerRequest(request.downloadInvididualRequest);
			Stream zipStream = await sender.Send(command, cancellationToken);
			return Results.File(
				zipStream,
				"application/zip",
				$"{request.downloadInvididualRequest.SubjectName}.zip");
		})
		.WithName("DownloadIndividualReport")
		.WithTags("ATS")
		.Produces(StatusCodes.Status200OK, contentType: "application/zip")
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Download Individual Report")
		.WithDescription("Downloads the selected documents as a ZIP archive.")
		.RequireAuthorization();
	}
}
