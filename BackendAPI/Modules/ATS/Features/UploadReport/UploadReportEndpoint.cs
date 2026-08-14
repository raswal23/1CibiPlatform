namespace ATS.Features.UploadReport;

public record UploadReportRequest(ReportDetailsDTO ReportDetailsDTO);

public record UploadReportResponse(bool IsUploaded);

public class UploadReportEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("uploadreport", async (
			[FromForm] UploadReportRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new UploadReportCommand(request.ReportDetailsDTO);
			var result = await sender.Send(command, cancellationToken);
			var response = new UploadReportResponse(result.IsUploaded);
			return Results.Ok(response.IsUploaded);
		})
		.DisableAntiforgery()
		.WithName("UploadReport")
		.WithTags("ATS")
		.Produces<bool>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Upload Report")
		.WithDescription("Uploads and stores ATS report details. Replacing an existing report archives the previous record.")
		.RequireAuthorization();
	}
}
