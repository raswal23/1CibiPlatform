namespace Auth.Features.IsOtpValid;

public record IsOtpValidRequest(OtpVerificationRequestDTO OtpVerificationRequestDto);

public record IsOtpValidResponse(bool isOtpSessionValid);

public class IsOtpValidEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("verify/validate/otp", async (
			IsOtpValidRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new IsOtpValidCommand(request.OtpVerificationRequestDto);

			IsOtpValidResult result = await sender.Send(command, cancellationToken);

			var response = new IsOtpValidResponse(result.isOtpVerfied);

			return Results.Ok(response);
		})
		.WithName("isOtpVerified")
		.WithTags("Authentication")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("OTP Session")
		.WithDescription("OTP Session");
	}
}
