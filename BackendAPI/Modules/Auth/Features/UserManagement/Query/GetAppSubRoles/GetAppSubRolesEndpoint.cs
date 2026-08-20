namespace Auth.Features.UserManagement.Query.GetAppSubRoles;

public record GetAppSubRolesEndpointRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null);

public record GetAppSubRolesEndpointResponse(KeysetPaginatedResult<AppSubRolesDTO> AppSubRoles);
public class GetAppSubRolesEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("auth/getappsubroles", async (
			[AsParameters] GetAppSubRolesEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetAppSubRolesQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm);

			var applications = await sender.Send(query, cancellationToken);

			var result = new GetAppSubRolesEndpointResponse(applications.AppSubRoles);

			return Results.Ok(result);
		})
		.WithName("GetAppSubRoles")
		.WithTags("User Management")
		.Produces<GetAppSubRolesEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get AppSubRoles")
		.WithDescription("Retrieves a list of users with an application, role, and submenu")
		.RequireAuthorization();
	}
}

