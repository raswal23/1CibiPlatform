namespace ATS.Features.Web.EmailAccountManagement.Command.DeleteEmailAccount;

public record DeleteEmailAccountRequest(DeleteEmailAccountDTO deleteEmailAccount);

public record DeleteEmailAccountResponse(EmailAccountOtpSentDTO otpSent);

public class DeleteEmailAccountEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		// POST rather than DELETE: this call sends a code, it does not remove anything. The
		// removal happens on verifyemailaccountotp with a Delete purpose.
		app.MapPost("deleteemailaccount", async (
			DeleteEmailAccountRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new DeleteEmailAccountCommand(request.deleteEmailAccount);
			var result = await sender.Send(command, cancellationToken);

			return Results.Ok(new DeleteEmailAccountResponse(result.otpSent));
		})
		.WithName("DeleteEmailAccount")
		.WithTags("ATS")
		.Produces<DeleteEmailAccountResponse>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		// While a send is in flight through this account.
		.ProducesProblem(StatusCodes.Status409Conflict)
		.WithSummary("Delete Email Account")
		.WithDescription("Sends a verification code to the account's own mailbox to confirm its removal.")
		.RequireAuthorization();
	}
}
