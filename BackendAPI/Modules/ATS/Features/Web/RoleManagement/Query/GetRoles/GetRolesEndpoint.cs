namespace ATS.Features.Web.RoleManagement.Query.GetRoles;

public record GetRolesEndpointRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null);

public record GetRolesEndpointResponse(KeysetPaginatedResult<RoleDetailsDTO> Roles);

public class GetRolesEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getroles", async (
			[AsParameters] GetRolesEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetRolesQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new GetRolesEndpointResponse(result.Roles));
		})
		.WithName("ATSGetRoles")
		.WithTags("Role Management")
		.Produces<GetRolesEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Roles")
		.WithDescription("Retrieves a list of ATS roles.")
		.RequireAuthorization();
	}
}
