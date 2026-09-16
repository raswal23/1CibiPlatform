namespace ATS.Features.Web.Notifications.Query.GetUnreadNotificationCount;

public record GetUnreadNotificationCountEndpointResponse(NotificationUnreadCountDTO Count);

public class GetUnreadNotificationCountEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getunreadnotificationcount", async (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(
				new GetUnreadNotificationCountQueryRequest(),
				cancellationToken);

			return Results.Ok(new GetUnreadNotificationCountEndpointResponse(result.Count));
		})
		.WithName("GetUnreadNotificationCount")
		.WithTags("ATS")
		.Produces<GetUnreadNotificationCountEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Unread Notification Count")
		.WithDescription(
			"Returns how many unread in-app notifications the authenticated caller has. "
			+ "Read once on load to seed the bell badge; afterwards the count is kept "
			+ "current over SignalR rather than by polling.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
