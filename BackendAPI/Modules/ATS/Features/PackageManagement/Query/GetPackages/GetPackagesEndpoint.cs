namespace ATS.Features.PackageManagement.Query.GetPackages;

public record GetPackagesEndpointRequest(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null);

public record GetPackagesEndpointResponse(PaginatedResult<PackageDetailsDTO> Packages);

public class GetPackagesEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getpackages", async (
			[AsParameters] GetPackagesEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetPackagesQueryRequest(
				request.PageNumber,
				request.PageSize,
				request.SearchTerm);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetPackagesEndpointResponse(result.Packages));
		})
		.WithName("GetPackages")
		.WithTags("ATS")
		.Produces<GetPackagesEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Packages")
		.WithDescription("Retrieves a list of packages.")
		.RequireAuthorization();
	}
}
