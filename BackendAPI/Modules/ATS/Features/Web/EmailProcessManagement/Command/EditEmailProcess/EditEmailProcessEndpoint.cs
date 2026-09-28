namespace ATS.Features.Web.EmailProcessManagement.Command.EditEmailProcess;

public record EditEmailProcessRequest(EditEmailProcessDTO editEmailProcess);

public record EditEmailProcessResponse(EmailProcessDetailsDTO emailProcess);

public class EditEmailProcessEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("editemailprocess", async (
			EditEmailProcessRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new EditEmailProcessCommand(request.editEmailProcess);
			EditEmailProcessResult result = await sender.Send(command, cancellationToken);
			var response = new EditEmailProcessResponse(result.emailProcess);

			return Results.Ok(response.emailProcess);
		})
		.WithName("EditEmailProcess")
		.WithTags("Email Process Management")
		.Produces<EmailProcessDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Edit Email Process")
		.WithDescription("Replaces the CC copy list of an ATS notice, and turns it on or off.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
