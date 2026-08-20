namespace ATS.Features.AIAssistant.Command.ConfirmOrderDraft;

public record ConfirmOrderDraftEndpointRequest(Guid DraftId);

public record ConfirmOrderDraftEndpointResponse(AtsChatAnswerDTO Answer);

public class ConfirmOrderDraftEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("confirmorderdraft", async (
			ConfirmOrderDraftEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new ConfirmOrderDraftCommand(request.DraftId);

			var result = await sender.Send(command, cancellationToken);

			return Results.Ok(new ConfirmOrderDraftEndpointResponse(result.Answer));
		})
		.WithName("ConfirmOrderDraft")
		.WithTags("ATS")
		.Produces<ConfirmOrderDraftEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Confirm a staged ATS order")
		.WithDescription("Creates the order that the ATS assistant staged, and sends the email "
			+ "invitation to the candidate. Drafts are single use and expire.")
		.RequireAuthorization();
	}
}
