namespace ATS.Features.UserManagement.Query.GetUsers;

public record GetUsersRequest(PaginationRequest paginationRequest);

public record GetUsersResponse(PaginatedResult<UserDetailsDTO> users);

public class GetUsersEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getusers", async (
			int pageIndex = 1,
			int pageSize = 10,
			string? search = null,
			ISender sender = null!,
			CancellationToken cancellationToken = default) =>
		{
			var paginationRequest = new PaginationRequest
			{
				PageIndex = pageIndex,
				PageSize = pageSize,
				SearchTerm = search
			};

			var query = new GetUsersQuery(paginationRequest);
			var result = await sender.Send(query, cancellationToken);
			var response = new GetUsersResponse(result.users);
			return Results.Ok(response.users);
		})
		.WithName("ATSGetUsers")
		.WithTags("ATS User Management")
		.Produces<PaginatedResult<UserDetailsDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get ATS Users")
		.WithDescription("Retrieves a paginated list of ATS users and their module assignments.")
		.RequireAuthorization();
	}
}
