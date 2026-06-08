namespace ATS.Features.DownloadBulkTemplate;

public record DownloadBulkTemplateRequest();

public record DownloadBulkTemplateResponse(string templateLink);

public class DownloadBulkTemplateEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("downloadbulktemplate", async (ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new DownloadBulkTemplateCommand();
			DownloadBulkTemplateResult result = await sender.Send(command, cancellationToken);
			var response = new DownloadBulkTemplateResponse(result.templateLink);
			return Results.Json(response);
		})
		  .WithName("DownloadBulkTemplate")
		  .WithTags("ATS")
		  .Produces<DownloadBulkTemplateResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Download Bulk Template")
		  .WithDescription("Downloads the bulk template for inserting multiple subject entries.");
	}
}
