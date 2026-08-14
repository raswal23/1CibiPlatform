namespace Auth.Features.ResendOTP;

public record ResendOTPEndpointRequest(OtpVerificationRequestDTO OtpVerificationRequestDto);

public record ResendOTPEndpointResponse(bool IsSuccess);


public class ResendOTPEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("/verify/resend-otp", async (
			ISender sender,
			ResendOTPEndpointRequest request) =>
		{
			var command = new ResendOTPCommand(request.OtpVerificationRequestDto);

			var result = await sender.Send(command);

			return Results.Ok(result);
		})
		.WithName("manual-resend-otp")
		.WithTags("Authentication")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("manual-resend-otp")
		.WithDescription("manual-resend-otp");
	}
}
