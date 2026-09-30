namespace ATS.Features.Web.Notifications.Command.MarkNotificationRead;

public record MarkNotificationReadEndpointRequest(Guid NotificationId);

public record MarkNotificationReadEndpointResponse(bool Success);

public class MarkNotificationReadEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("marknotificationread", async (
			MarkNotificationReadEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new MarkNotificationReadCommand(request.NotificationId);

			var result = await sender.Send(command, cancellationToken);

			var response = new MarkNotificationReadEndpointResponse(result.Success);

			return Results.Ok(response.Success);
		})
		.WithName("MarkNotificationRead")
		.WithTags("ATS")
		.Produces<bool>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Mark Notification Read")
		.WithDescription(
			"Marks one of the caller's own notifications as read. Returns false when the "
			+ "notification is unknown, already read, or belongs to another user - the "
			+ "three are deliberately indistinguishable so the response cannot be used to "
			+ "probe for somebody else's notification ids.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
