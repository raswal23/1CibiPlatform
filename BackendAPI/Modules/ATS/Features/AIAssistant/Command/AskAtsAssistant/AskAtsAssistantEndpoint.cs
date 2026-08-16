namespace ATS.Features.AIAssistant.Command.AskAtsAssistant;

public record AskAtsAssistantEndpointRequest(string Question);

public record AskAtsAssistantEndpointResponse(AtsChatAnswerDTO Answer);

public class AskAtsAssistantEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("askatsassistant", async (
			AskAtsAssistantEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new AskAtsAssistantCommand(request.Question);

			var result = await sender.Send(command, cancellationToken);

			return Results.Ok(new AskAtsAssistantEndpointResponse(result.Answer));
		})
		.WithName("AskAtsAssistant")
		.WithTags("ATS")
		.Produces<AskAtsAssistantEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.WithSummary("Ask the ATS assistant")
		.WithDescription("Sends a question to the ATS assistant, which can look up orders by "
			+ "candidate name and stage a new order for confirmation.")
		.RequireAuthorization();
	}
}
