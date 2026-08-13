namespace ATS.Features.MarkAsDisputed;

public record MarkAsDisputedRequest(DisputeOrderRequestDTO disputeRequest);

public record MarkAsDisputedResponse(bool Success);

public class MarkAsDisputedEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("markasdisputed", async (
			MarkAsDisputedRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new MarkAsDisputedCommand(request.disputeRequest);
			var result = await sender.Send(command, cancellationToken);

			if (!result.Success)
			{
				return Results.NotFound("Email invitation request not found.");
			}

			var response = new MarkAsDisputedResponse(result.Success);
			return Results.Ok(response.Success);
		})
		.WithName("MarkAsDisputed")
		.WithTags("ATS")
		.Produces<bool>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.Produces(StatusCodes.Status401Unauthorized)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Mark As Disputed")
		.WithDescription("Marks an order as disputed by setting IsDisputed to true and DisputedAt to current UTC time.")
		.RequireAuthorization();
	}
}
