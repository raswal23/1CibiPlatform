namespace ATS.Features.Web.Notifications.Command.MarkAllNotificationsRead;

public record MarkAllNotificationsReadEndpointResponse(int UpdatedCount);

public class MarkAllNotificationsReadEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("markallnotificationsread", async (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(
				new MarkAllNotificationsReadCommand(),
				cancellationToken);

			var response = new MarkAllNotificationsReadEndpointResponse(result.UpdatedCount);

			return Results.Ok(response.UpdatedCount);
		})
		.WithName("MarkAllNotificationsRead")
		.WithTags("ATS")
		.Produces<int>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Mark All Notifications Read")
		.WithDescription(
			"Marks every unread notification belonging to the authenticated caller as "
			+ "read and returns how many changed. Scoped to the caller's own inbox; there "
			+ "is no parameter that can widen it.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
