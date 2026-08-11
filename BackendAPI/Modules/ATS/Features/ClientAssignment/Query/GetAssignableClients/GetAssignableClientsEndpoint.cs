namespace ATS.Features.ClientAssignment.Query.GetAssignableClients;

public sealed class GetAssignableClientsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getassignableclients", async (
			int pageIndex = 1,
			int pageSize = 25,
			string? search = null,
			ISender sender = null!,
			CancellationToken cancellationToken = default) =>
		{
			var result = await sender.Send(
				new GetAssignableClientsQuery(new PaginationRequest(
					pageIndex,
					pageSize,
					search?.Trim())),
				cancellationToken);
			return Results.Ok(result.Clients);
		})
		.WithName("ATSGetAssignableClients")
		.WithTags("ATS Client Assigning")
		.Produces<PaginatedResult<ClientLookupDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Search assignable ATS clients")
		.WithDescription("Returns a bounded page of active clients for the assignment picker.")
		.RequireAuthorization();
	}
}
