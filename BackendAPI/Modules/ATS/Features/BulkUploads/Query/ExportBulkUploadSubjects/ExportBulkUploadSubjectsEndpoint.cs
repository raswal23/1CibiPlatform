namespace ATS.Features.BulkUploads.Query.ExportBulkUploadSubjects;

public record ExportBulkUploadSubjectsEndpointRequest(Guid FileId);

public class ExportBulkUploadSubjectsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("exportbulkuploadsubjects", async (
			[AsParameters] ExportBulkUploadSubjectsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new ExportBulkUploadSubjectsQueryRequest(request.FileId);

			var result = await sender.Send(query, cancellationToken);

			// The filename is built by the service from the stored file name, never
			// from caller input.
			return Results.File(
				result.Export.Content,
				"text/csv",
				result.Export.FileName);
		})
		.WithName("ExportBulkUploadSubjects")
		.WithTags("ATS")
		.Produces(StatusCodes.Status200OK, contentType: "text/csv")
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Export Bulk Upload Subjects")
		.WithDescription(
			"Downloads every subject of a single bulk upload file as CSV, including each "
			+ "invitation's email, application form and order status.")
		.RequireAuthorization();
	}
}
