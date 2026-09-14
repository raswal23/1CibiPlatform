namespace ATS.Features.Web.DownloadApplicationFormPreview;

public record DownloadApplicationFormPreviewEndpointRequest(Guid EmailInvitationRequestId);

public class DownloadApplicationFormPreviewEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("downloadapplicationformpreview", async (
			[AsParameters] DownloadApplicationFormPreviewEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new DownloadApplicationFormPreviewCommand(request.EmailInvitationRequestId);
			var result = await sender.Send(command, cancellationToken);
			// The file name is derived server-side from the order the caller was
			// actually allowed to read, not from anything they supplied.
			return Results.File(result.Pdf, "application/pdf", result.FileName);
		})
		.WithName("DownloadApplicationFormPreview")
		.WithTags("ATS")
		.Produces(StatusCodes.Status200OK, contentType: "application/pdf")
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Download Application Form Preview")
		.WithDescription("Renders the application form answers as a PDF matching the on-screen preview.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
