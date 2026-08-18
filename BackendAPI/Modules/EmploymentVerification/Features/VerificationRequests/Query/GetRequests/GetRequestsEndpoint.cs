namespace EmploymentVerification.Features.VerificationRequests.Query.GetRequests;

public sealed class GetRequestsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet(
				"api/employment-verification/requests",
				async (ISender sender, CancellationToken cancellationToken) =>
				{
					var result = await sender.Send(
						new GetRequestsQuery(),
						cancellationToken);

					return Results.Ok(result);
				})
			.RequireAuthorization()
			.WithTags("Employment Verification");
	}
}
