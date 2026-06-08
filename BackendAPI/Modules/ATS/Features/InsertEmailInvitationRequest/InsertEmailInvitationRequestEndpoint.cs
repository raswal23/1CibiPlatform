namespace ATS.Features.EmailInvitationRequest;

public record EmailInvitationRequestRequest(EmailInvitationRequestDTO emailInvitationRequestDTO);

public record EmailInvitationRequestResponse(bool isAdded);

public class InsertEmailInvitationRequestEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
			app.MapPost("insertEmailInvitationRequest", async (EmailInvitationRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
			{
				var command = new EmailInvitationRequestCommand(request.emailInvitationRequestDTO);
				EmailInvitationRequestResult result = await sender.Send(command, cancellationToken);
				var response = new EmailInvitationRequestResponse(result.isAdded);
				return Results.Ok(response.isAdded);

			})
		  .WithName("InsertEmailInvitationRequest")
		  .WithTags("ATS")
		  .Produces<EmailInvitationRequestResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Insert Subject")
		  .WithDescription("Inserts a new subject entry to the database.")
		  .RequireAuthorization();
	}
}
