namespace ATS.Features.Web.UserManagement.Query.GetMyAccess;

public record GetMyAccessResponse(int RoleId, int? ClientId);

public class GetMyAccessEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("get-my-access", async (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(new GetMyAccessQuery(), cancellationToken);
			return Results.Ok(new GetMyAccessResponse(result.RoleId, result.ClientId));
		})
		.WithName("ATSGetMyAccess")
		.WithTags("ATS User Management")
		.Produces<GetMyAccessResponse>()
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.WithSummary("Get the authenticated user's ATS role and client access")
		.RequireAuthorization();
	}
}
