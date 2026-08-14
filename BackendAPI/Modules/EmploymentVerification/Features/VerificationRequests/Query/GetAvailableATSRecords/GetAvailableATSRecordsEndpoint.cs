namespace EmploymentVerification.Features.VerificationRequests.Query.GetAvailableATSRecords;

public sealed class GetAvailableATSRecordsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet(
				"api/employment-verification/ats/in-progress",
				async (ISender sender, CancellationToken cancellationToken) =>
				{
					var result = await sender.Send(
						new GetAvailableATSRecordsQuery(),
						cancellationToken);

					return Results.Ok(result);
				})
			.RequireAuthorization()
			.WithTags("Employment Verification");
	}
}
