namespace ATS.Features.Web.EmailAccountManagement.Command.RegisterEmailAccount;

public record RegisterEmailAccountRequest(RegisterEmailAccountDTO emailAccount);

public record RegisterEmailAccountResponse(EmailAccountOtpSentDTO otpSent);

public class RegisterEmailAccountEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("registeremailaccount", async (
			RegisterEmailAccountRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new RegisterEmailAccountCommand(request.emailAccount);
			var result = await sender.Send(command, cancellationToken);

			// The account exists but is Pending, so 200 rather than 201: nothing usable was
			// created until the code comes back.
			return Results.Ok(new RegisterEmailAccountResponse(result.otpSent));
		})
		.WithName("RegisterEmailAccount")
		.WithTags("ATS")
		.Produces<RegisterEmailAccountResponse>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status409Conflict)
		.WithSummary("Register Email Account")
		.WithDescription("Registers a sender account as Pending and sends a verification code through its own credentials.")
		.RequireAuthorization();
	}
}
