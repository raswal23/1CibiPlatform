namespace EmploymentVerification.Features.VerificationRequests.Query.GetSentRequests;

public sealed class GetSentRequestsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet(
				"api/employment-verification/requests/sent",
				async (ISender sender, CancellationToken cancellationToken) =>
				{
					var result = await sender.Send(
						new GetSentRequestsQuery(),
						cancellationToken);

					return Results.Ok(result);
				})
			.RequireAuthorization()
			.WithName("GetSentEmploymentVerificationRequests")
			.WithTags("Employment Verification")
			.Produces<IReadOnlyList<SentVerificationRequestDTO>>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.WithSummary("Lists raised employment verification requests")
			.WithDescription(
				"Returns every verification request raised from this module, "
				+ "with its current status and response timestamps, for the tracking view.");
	}
}
