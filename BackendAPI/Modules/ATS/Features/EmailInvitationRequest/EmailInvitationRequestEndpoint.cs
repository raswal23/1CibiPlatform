namespace ATS.Features.EmailInvitationRequest;

public record EmailInvitationRequestRequest(EmailInvitationRequestDTO emailInvitationRequestDTO);

public record EmailInvitationRequestResponse(Guid EmailInvitationID);

public class EmailInvitationRequestEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
			app.MapPost("insertEmailInvitationRequest", async (EmailInvitationRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
			{
				var command = new EmailInvitationRequestCommand(request.emailInvitationRequestDTO);
				EmailInvitationRequestResult result = await sender.Send(command, cancellationToken);
				var response = new EmailInvitationRequestResponse(result.EmailInvitationID);
				return Results.Created(string.Empty, response);

			})
		  .WithName("InsertEmailInvitationRequest")
		  .WithTags("ATS")
		  .Produces<EmailInvitationRequestResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Insert Subject")
		  .WithDescription("Inserts a new subject entry to the database.");
	}
}
