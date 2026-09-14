namespace ATS.Features.Web.EmailAccountManagement.Command.VerifyEmailAccountOtp;

public record VerifyEmailAccountOtpRequest(VerifyEmailAccountOtpDTO verification);

public record VerifyEmailAccountOtpResponse(EmailAccountOtpResultDTO result);

public class VerifyEmailAccountOtpEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("verifyemailaccountotp", async (
			VerifyEmailAccountOtpRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new VerifyEmailAccountOtpCommand(request.verification);
			var result = await sender.Send(command, cancellationToken);

			// 200 even for a wrong code. The result carries IsVerified and the remaining
			// attempts, which the dialog needs to show the cap closing - a 400 would collapse
			// "that code is wrong, 3 tries left" into an error with nothing to display.
			return Results.Ok(new VerifyEmailAccountOtpResponse(result.result));
		})
		.WithName("VerifyEmailAccountOtp")
		.WithTags("ATS")
		.Produces<VerifyEmailAccountOtpResponse>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Verify Email Account OTP")
		.WithDescription("Confirms a code and applies the registration, edit or deletion it was approving.")
		.RequireAuthorization();
	}
}
