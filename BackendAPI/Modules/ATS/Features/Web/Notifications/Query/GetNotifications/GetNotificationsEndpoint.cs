namespace ATS.Features.Web.Notifications.Query.GetNotifications;

public record GetNotificationsEndpointRequest(
	string? Cursor = null,
	int? PageSize = 15,
	bool UnreadOnly = false);

public record GetNotificationsEndpointResponse(KeysetPaginatedResult<NotificationListDTO> Notifications);

public class GetNotificationsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getnotifications", async (
			[AsParameters] GetNotificationsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetNotificationsQueryRequest(
				request.Cursor,
				request.PageSize,
				request.UnreadOnly);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetNotificationsEndpointResponse(result.Notifications));
		})
		.WithName("GetNotifications")
		.WithTags("ATS")
		.Produces<GetNotificationsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Notifications")
		.WithDescription(
			"Retrieves the caller's own in-app notifications, newest first, with keyset "
			+ "pagination for the infinite-scroll notifications page. The recipient is "
			+ "always the authenticated user - there is no parameter to read another "
			+ "user's inbox.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
