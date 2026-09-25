namespace ATS.Features.Web.EmailProcessManagement.Command.AddEmailProcess;

public record AddEmailProcessRequest(AddEmailProcessDTO emailProcess);

public record AddEmailProcessResponse(EmailProcessDetailsDTO emailProcess);

public class AddEmailProcessEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("addemailprocess", async (
			AddEmailProcessRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new AddEmailProcessCommand(request.emailProcess);
			AddEmailProcessResult result = await sender.Send(command, cancellationToken);
			var response = new AddEmailProcessResponse(result.emailProcess);

			return Results.Ok(response.emailProcess);
		})
		.WithName("AddEmailProcess")
		.WithTags("Email Process Management")
		.Produces<EmailProcessDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add Email Process")
		.WithDescription("Registers the CC copy list for an ATS notice that does not have one yet.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
