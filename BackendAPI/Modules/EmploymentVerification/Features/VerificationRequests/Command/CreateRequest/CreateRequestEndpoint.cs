namespace EmploymentVerification.Features.VerificationRequests.Command.CreateRequest;

public sealed class CreateRequestEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost(
				"api/employment-verification/requests",
				async (
					CreateEmploymentVerificationRequest request,
					ISender sender,
					CancellationToken cancellationToken) =>
				{
					var command = new CreateRequestCommand(request);
					var result = await sender.Send(command, cancellationToken);

					return Results.Ok(result);
				})
			.RequireAuthorization()
			.WithTags("Employment Verification");
	}
}
