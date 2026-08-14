namespace ATS.Features.WithdrawnApplicationForm;

public record WithdrawnApplicationFormRequest(string HashToken);

public record WithdrawnApplicationFormResponse(bool isEdited);

public class WithdrawnApplicationFormEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("withdrawnapplicationform", async (WithdrawnApplicationFormRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new WithdrawnApplicationFormCommand(request.HashToken);
			WithdrawnApplicationFormResult result = await sender.Send(command, cancellationToken);
			var response = new WithdrawnApplicationFormResponse(result.isEdited);
			return Results.Ok(response.isEdited);

		})
		.WithName("WithdrawnApplicationForm")
		.WithTags("ATS")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Withdrawn Application Form")
		.WithDescription("Handles the withdrawn application form process.");
	}
}
