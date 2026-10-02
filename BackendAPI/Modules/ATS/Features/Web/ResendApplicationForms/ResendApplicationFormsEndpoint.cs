namespace ATS.Features.Web.ResendApplicationForms;

public record ResendApplicationFormsEndpointRequest(IReadOnlyCollection<Guid> EmailInvitationIds);

public record ResendApplicationFormsEndpointResponse(
	int RequestedCount,
	int RequeuedCount,
	bool IsComplete);

public class ResendApplicationFormsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("resendapplicationforms", async (
			ResendApplicationFormsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new ResendApplicationFormsCommand(request.EmailInvitationIds ?? []);

			var result = await sender.Send(command, cancellationToken);

			var response = new ResendApplicationFormsEndpointResponse(
				result.RequestedCount,
				result.RequeuedCount,
				result.IsComplete);

			// The full result, not a bool: a selection made a minute ago can contain
			// invitations the email job has since picked up, so "3 of 5" is a normal
			// outcome the operator has to be able to see.
			return Results.Ok(response);
		})
		.WithName("ResendApplicationForms")
		.WithTags("ATS")
		.Produces<ResendApplicationFormsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Resend Application Forms")
		.WithDescription(
			"Queues a fresh application form for every selected invitation, each with its "
			+ "own new token and a reset send-attempt budget. Invitations outside the "
			+ "caller's scope are ignored, and any the email job is actively sending are "
			+ "skipped, so the response reports how many of the requested invitations "
			+ "actually moved. Returns 400 above the batch limit and 404 when nothing in "
			+ "the selection is available to the caller.")
		.RequireAuthorization();
	}
}
