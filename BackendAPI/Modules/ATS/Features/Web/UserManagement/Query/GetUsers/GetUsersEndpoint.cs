namespace ATS.Features.Web.UserManagement.Query.GetUsers;

public record GetUsersRequest(KeysetPaginationRequest KeysetPaginationRequest);

public record GetUsersResponse(KeysetPaginatedResult<UserDetailsDTO> users);

public class GetUsersEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getusers", async (
			string? cursor = null, int pageSize = 10,
			string? search = null,
			ISender sender = null!,
			CancellationToken cancellationToken = default) =>
		{
			var KeysetPaginationRequest = new KeysetPaginationRequest
			{
				Cursor = cursor,
				PageSize = pageSize,
				SearchTerm = search
			};

			var query = new GetUsersQuery(KeysetPaginationRequest);
			var result = await sender.Send(query, cancellationToken);
			var response = new GetUsersResponse(result.users);
			return Results.Ok(response.users);
		})
		.WithName("ATSGetUsers")
		.WithTags("ATS User Management")
		.Produces<KeysetPaginatedResult<UserDetailsDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get ATS Users")
		.WithDescription("Retrieves a paginated list of ATS users and their module assignments.")
		.RequireAuthorization();
	}
}
