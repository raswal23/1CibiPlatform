namespace ATS.Features.Web.UserManagement.Query.GetAuthUsers;

public record GetAuthUsersResponse(IReadOnlyList<ATSUserLookupDTO> users);

public class GetAuthUsersEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getauthusers", async (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(new GetAuthUsersQuery(), cancellationToken);
			return Results.Ok(new GetAuthUsersResponse(result.users).users);
		})
		.WithName("ATSGetAuthUsers")
		.WithTags("ATS User Management")
		.Produces<IReadOnlyList<ATSUserLookupDTO>>()
		.WithSummary("Get Auth users assigned to ATS")
		.WithDescription("Retrieves active Auth users assigned to ATS through submenu 7.")
		.RequireAuthorization();
	}
}
