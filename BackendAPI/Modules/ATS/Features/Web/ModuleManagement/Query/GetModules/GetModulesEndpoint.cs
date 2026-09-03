namespace ATS.Features.Web.ModuleManagement.Query.GetModules;

public record GetModulesEndpointRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null);

public record GetModulesEndpointResponse(KeysetPaginatedResult<ModuleDetailsDTO> Modules);

public class GetModulesEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getmodules", async (
			[AsParameters] GetModulesEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetModulesQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetModulesEndpointResponse(result.Modules));
		})
		.WithName("ATSGetModules")
		.WithTags("Module Management")
		.Produces<GetModulesEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Modules")
		.WithDescription("Retrieves a list of ATS modules.")
		.RequireAuthorization();
	}
}
