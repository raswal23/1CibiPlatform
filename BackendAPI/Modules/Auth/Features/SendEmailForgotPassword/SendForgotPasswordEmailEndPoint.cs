namespace Auth.Features.SendEmailForgotPassword;

public record SendForgotPasswordEmailRequest(SendForgotPasswordEmailRequestDTO sendForgotPasswordEmailRequestDTO);

public record SendForgotPasswordEmailResponse(bool IsEmailSent);

public class SendForgotPasswordEmailEndPoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("forgot-password-email-send", async (
			SendForgotPasswordEmailRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new SendForgotPasswordEmailCommand(request.sendForgotPasswordEmailRequestDTO);

			SendForgotPasswordEmailResult result = await sender.Send(command, cancellationToken);

			var response = new SendForgotPasswordEmailResponse(result.IsEmailSent);

			return Results.Ok(response);
		})
		  .WithName("SendForgotPasswordEmail")
		  .WithTags("Authentication")
		  .Produces<SendForgotPasswordEmailResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Request password reset email")
		  .WithDescription("Triggers a password reset email if the account exists.");
	}
}