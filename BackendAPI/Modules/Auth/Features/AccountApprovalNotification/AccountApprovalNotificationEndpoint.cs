namespace Auth.Features.AccountApprovalNotification;

public record AccountApprovalNotificationRequest(string Gmail);

public record AccountApprovalNotificationResponse(bool IsSent);

public class AccountApprovalNotificationEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("account/approvalnotification", async (
			AccountApprovalNotificationRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new AccountApprovalNotificationCommand(request.Gmail);

			AccountApprovalNotificationResult result = await sender.Send(command);

			var response = new AccountApprovalNotificationResponse(result.IsSent);

			return Results.Ok(response.IsSent);

		})
		.WithName("AccountApprovalNotification")
		.WithTags("User Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("accountnotification")
		.WithDescription("accountnotification");
	}
}