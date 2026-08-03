namespace Auth.Features.VerifyOtp;

public record VerifiyOtpRequest(OtpRequestDTO OtpRequestDTO);

public record VerifiyOtpResponse(bool IsVerified);

public class VerifiyOtpEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("verify/otp", async (
			VerifiyOtpRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new VerifyOtpCommand(request.OtpRequestDTO);

			var result = await sender.Send(command);

			return new VerifiyOtpResponse(result.IsVerified);

		})
		.WithName("verifyotp")
		.WithTags("Authentication")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("verifyotp")
		.WithDescription("verifyotp");
	}
}
