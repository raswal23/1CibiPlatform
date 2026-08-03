namespace Auth.Features.AccountAssignmentNotification;
public record AccountNotificationRequest(AccountNotificationDTO AccountNotificationDTO);

public record AccountNotificationResponse(bool IsSent);

public class AccountAssignmentNotificationEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("account/notification", async (
			AccountNotificationRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new AccountNotificationCommand(request.AccountNotificationDTO);

			AccountNotificationResult result = await sender.Send(command);

			var response = new AccountNotificationResponse(result.IsSent);

			return Results.Ok(response.IsSent);

		})
		.WithName("AccountAssignmentNotification")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("accountnotification")
		.WithDescription("accountnotification");
	}
}
