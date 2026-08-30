namespace ATS.Features.Web.UserManagement.Query.GetMyModules;

public class GetMyModulesEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getmymodules", async (
			HttpContext httpContext,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
				?? httpContext.User.FindFirstValue("userId");
			if (!Guid.TryParse(userIdValue, out var userId))
				return Results.Unauthorized();

			var result = await sender.Send(new GetMyModulesQuery(userId), cancellationToken);
			return Results.Ok(result.ModuleIds);
		})
		.WithName("ATSGetMyModules")
		.WithTags("ATS User Management")
		.Produces<IReadOnlyList<int>>()
		.Produces(StatusCodes.Status401Unauthorized)
		.WithSummary("Get the authenticated ATS user's active modules")
		.RequireAuthorization();
	}
}
