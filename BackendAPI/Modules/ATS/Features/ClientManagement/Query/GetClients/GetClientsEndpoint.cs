namespace ATS.Features.ClientManagement.Query.GetClients;

public record GetClientsRequest(PaginationRequest paginationRequest);

public record GetClientsResponse(PaginatedResult<ClientDetailsDTO> clients);

public class GetClientsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getclients", async (int pageIndex = 1, int pageSize = 10, string? search = null, ISender sender = null!, CancellationToken cancellationToken = default) =>
		{
			var paginationRequest = new PaginationRequest
			{
				PageIndex = pageIndex,
				PageSize = pageSize,
				SearchTerm = search
			};

			var query = new GetClientsQuery(paginationRequest);
			GetClientsResult result = await sender.Send(query, cancellationToken);
			var response = new GetClientsResponse(result.clients);
			return Results.Ok(response.clients);
		})
		.WithName("GetClients")
		.WithTags("Client Management")
		.Produces<PaginatedResult<ClientDetailsDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Clients")
		.WithDescription("Retrieves a paginated list of clients.")
		.RequireAuthorization();
	}
}
