namespace Auth.Features.UserManagement.Query.GetUnApprovedUsers;
public record GetUnApprovedUsersEndpointRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null);

public record GetUnApprovedUsersEndpointResponse(KeysetPaginatedResult<UsersDTO> Users);
public class GetUnapprovedUsersEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("auth/getunapprovedusers", async (
			[AsParameters] GetUnApprovedUsersEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetUnApprovedUsersQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm);

			var users = await sender.Send(query, cancellationToken);

			var result = new GetUnApprovedUsersQueryResult(users.Users);

			return Results.Ok(result);
		})
		.WithName("GetUnApprovedUsers")
		.WithTags("User Management")
		.Produces<GetUnApprovedUsersEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get UnApproved Users")
		.WithDescription("Retrieves a list of unapproved users.")
		.RequireAuthorization();
	}
}
