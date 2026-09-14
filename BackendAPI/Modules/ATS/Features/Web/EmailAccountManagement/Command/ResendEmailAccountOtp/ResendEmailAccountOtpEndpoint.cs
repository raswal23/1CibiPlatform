namespace ATS.Features.Web.EmailAccountManagement.Command.ResendEmailAccountOtp;

public record ResendEmailAccountOtpRequest(ResendEmailAccountOtpDTO resend);

public record ResendEmailAccountOtpResponse(EmailAccountOtpSentDTO otpSent);

public class ResendEmailAccountOtpEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("resendemailaccountotp", async (
			ResendEmailAccountOtpRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new ResendEmailAccountOtpCommand(request.resend);
			var result = await sender.Send(command, cancellationToken);

			return Results.Ok(new ResendEmailAccountOtpResponse(result.otpSent));
		})
		.WithName("ResendEmailAccountOtp")
		.WithTags("ATS")
		.Produces<ResendEmailAccountOtpResponse>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Resend Email Account OTP")
		.WithDescription("Invalidates outstanding codes for an account and purpose, then sends a fresh one.")
		.RequireAuthorization();
	}
}
