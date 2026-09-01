namespace ATS.Features.Web.ResendApplicationForm;

public record ResendApplicationFormRequest(Guid emailInvitationId);

public record ResendApplicationFormResponse(bool Success);

public class ResendApplicationFormEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("resendapplicationform", async (
			ResendApplicationFormRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new ResendApplicationFormCommand(request.emailInvitationId);

			var result = await sender.Send(command, cancellationToken);

			var response = new ResendApplicationFormResponse(result.Success);

			return Results.Ok(response.Success);
		})
		.WithName("ResendApplicationForm")
		.WithTags("ATS")
		.Produces<bool>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Resend Application Form")
		.WithDescription("Resends an application form to a candidate by generating a new hash token and resetting the ticket status to 'Pending Candidate Info'.")
		.RequireAuthorization();
	}
}
