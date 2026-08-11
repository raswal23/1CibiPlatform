namespace ATS.Features.UserManagement.Query.GetUserClientAssignments;

public record GetUserClientAssignmentsResponse(IReadOnlyList<UserClientDetailsDTO> assignments);

public class GetUserClientAssignmentsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getuserclientassignments", async (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(new GetUserClientAssignmentsQuery(), cancellationToken);
			return Results.Ok(new GetUserClientAssignmentsResponse(result.assignments).assignments);
		})
		.WithName("ATSGetUserClientAssignments")
		.WithTags("ATS User Management")
		.Produces<IReadOnlyList<UserClientDetailsDTO>>()
		.WithSummary("Get ATS user-client assignments")
		.WithDescription("Retrieves the client assigned to each ATS Auth user.")
		.RequireAuthorization();
	}
}
