namespace ATS.Features.ClientAssignment.Query.GetClientAssignments;

public sealed class GetClientAssignmentsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getclientassignments", async (
			int pageIndex = 1,
			int pageSize = 10,
			string? search = null,
			HttpContext httpContext = null!,
			IUserManagementService userManagementService = null!,
			ISender sender = null!,
			CancellationToken cancellationToken = default) =>
		{
			if (!await ClientAssignmentAccess.CanManageAsync(
				httpContext,
				userManagementService,
				cancellationToken))
				return Results.Forbid();

			var result = await sender.Send(
				new GetClientAssignmentsQuery(new PaginationRequest(
					pageIndex,
					pageSize,
					search?.Trim())),
				cancellationToken);
			return Results.Ok(result.Assignments);
		})
		.WithName("ATSGetClientAssignments")
		.WithTags("ATS Client Assigning")
		.Produces<PaginatedResult<ClientAssignmentDetailsDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.Produces(StatusCodes.Status403Forbidden)
		.WithSummary("Get ATS client assignments")
		.WithDescription("Returns active ATS users and their current client assignment using server-side search and pagination.")
		.RequireAuthorization();
	}
}
