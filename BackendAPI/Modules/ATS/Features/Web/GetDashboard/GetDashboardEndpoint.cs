namespace ATS.Features.Web.Dashboard;

public record GetDashboardEndpointRequest(string? Requester = null);

public record GetDashboardEndpointResponse(ATSDashboardDTO Dashboard);

public class GetDashboardEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getdashboard", async (
			[AsParameters] GetDashboardEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(
				new GetDashboardQueryRequest(request.Requester),
				cancellationToken);

			return Results.Ok(new GetDashboardEndpointResponse(result.Dashboard));
		})
		.WithName("GetATSDashboard")
		.WithTags("ATS")
		.Produces<GetDashboardEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get ATS dashboard data")
		.WithDescription("Retrieves ATS dashboard metrics, optionally filtered by requester.")
		.RequireAuthorization();
	}
}
