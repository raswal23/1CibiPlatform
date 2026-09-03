namespace ATS.Features.Web.UserManagement.Query.GetMyRoleId;

public record GetMyRoleIdResponse(int? RoleId);

public class GetMyRoleIdEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getmyroleid", async (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(new GetMyRoleIdQuery(), cancellationToken);
			return Results.Ok(new GetMyRoleIdResponse(result.RoleId));
		})
		.WithName("ATSGetMyRoleId")
		.WithTags("ATS User Management")
		.Produces<GetMyRoleIdResponse>()
		.WithSummary("Get the authenticated ATS user's active role")
		.RequireAuthorization();
	}
}
