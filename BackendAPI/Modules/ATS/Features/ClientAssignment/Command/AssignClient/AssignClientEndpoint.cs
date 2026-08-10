namespace ATS.Features.ClientAssignment.Command.AssignClient;

public record AssignClientRequest(AssignUserClientDTO Assignment);

public sealed class AssignClientEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPut("assignclient", async (
			AssignClientRequest request,
			HttpContext httpContext,
			IUserManagementService userManagementService,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			if (!await ClientAssignmentAccess.CanManageAsync(
				httpContext,
				userManagementService,
				cancellationToken))
				return Results.Forbid();

			var result = await sender.Send(
				new AssignClientCommand(request.Assignment),
				cancellationToken);
			return Results.Ok(result.Assignment);
		})
		.WithName("ATSAssignClient")
		.WithTags("ATS Client Assigning")
		.Produces<ClientAssignmentDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.Produces(StatusCodes.Status403Forbidden)
		.WithSummary("Assign or replace an ATS user's client")
		.WithDescription("Creates the user's first assignment or replaces the existing client. Re-selecting the current client is a no-op.")
		.RequireAuthorization();
	}
}
