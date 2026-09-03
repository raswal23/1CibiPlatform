namespace ATS.Features.PublicApi.GetPackages;

public record GetPackagesEndpointRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? SearchTerm = null);

public record GetPackagesEndpointResponse(KeysetPaginatedResult<PackageDetailsDTO> Packages);

public class GetPackagesEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("api/public/ats/packages", async (
			[AsParameters] GetPackagesEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetPackagesQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetPackagesEndpointResponse(result.Packages));
		})
		.WithName("PublicGetPackages")
		.WithTags("ATS Public API")
		.Produces<GetPackagesEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.WithSummary("List available packages")
		.WithDescription(
			"Returns the background-check packages the access token's client is "
			+ "entitled to. Use a package name from this list as the `package` field "
			+ "when creating an endorsement.")
		.RequireAuthorization();
	}
}
