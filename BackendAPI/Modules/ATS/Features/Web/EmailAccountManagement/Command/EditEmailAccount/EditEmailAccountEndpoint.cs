namespace ATS.Features.Web.EmailAccountManagement.Command.EditEmailAccount;

public record EditEmailAccountRequest(EditEmailAccountDTO editEmailAccount);

public record EditEmailAccountResponse(EmailAccountOtpSentDTO? otpSent, bool requiresVerification);

public class EditEmailAccountEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("editemailaccount", async (
			EditEmailAccountRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new EditEmailAccountCommand(request.editEmailAccount);
			var result = await sender.Send(command, cancellationToken);

			return Results.Ok(new EditEmailAccountResponse(
				result.otpSent,
				requiresVerification: result.otpSent is not null));
		})
		.WithName("EditEmailAccount")
		.WithTags("ATS")
		.Produces<EditEmailAccountResponse>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		// While a send is in flight through this account.
		.ProducesProblem(StatusCodes.Status409Conflict)
		.WithSummary("Edit Email Account")
		.WithDescription("Saves the safe fields immediately; sends a verification code when a credential field changed.")
		.RequireAuthorization();
	}
}
