namespace Auth.Features.UserManagement.Query.GetRoles;

public record GetRolesEndpointRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null);

public record GetRolesEndpointResponse(KeysetPaginatedResult<RolesDTO> Roles);
public class GetRolesEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("auth/getroles", async (
			[AsParameters] GetRolesEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetRolesQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm);

			var roles = await sender.Send(query, cancellationToken);

			var result = new GetRolesEndpointResponse(roles.Roles);

			return Results.Ok(result);
		})
		.WithName("GetRoles")
		.WithTags("User Management")
		.Produces<GetRolesEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Roles")
		.WithDescription("Retrieves a list of roles.")
		.RequireAuthorization();
	}
}

