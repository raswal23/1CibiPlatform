namespace ATS.Features.Web.ClientAssignment.Query.GetClientAssignments;

public sealed class GetClientAssignmentsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getclientassignments", async (
			string? cursor = null, int pageSize = 10,
			string? search = null,
			ISender sender = null!,
			CancellationToken cancellationToken = default) =>
		{
			var result = await sender.Send(
				new GetClientAssignmentsQuery(new KeysetPaginationRequest(
					cursor,
					pageSize,
					search?.Trim())),
				cancellationToken);
			return Results.Ok(result.Assignments);
		})
		.WithName("ATSGetClientAssignments")
		.WithTags("ATS Client Assigning")
		.Produces<KeysetPaginatedResult<ClientAssignmentDetailsDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get ATS client assignments")
		.WithDescription("Returns active ATS users and their current client assignment using server-side search and pagination.")
		.RequireAuthorization();
	}
}
