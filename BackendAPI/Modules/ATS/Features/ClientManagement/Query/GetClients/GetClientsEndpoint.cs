namespace ATS.Features.ClientManagement.Query.GetClients;

public record GetClientsRequest(KeysetPaginationRequest KeysetPaginationRequest);

public record GetClientsResponse(KeysetPaginatedResult<ClientDetailsDTO> clients);

public class GetClientsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getclients", async (string? cursor = null, int pageSize = 10, string? search = null, ISender sender = null!, CancellationToken cancellationToken = default) =>
		{
			var KeysetPaginationRequest = new KeysetPaginationRequest
			{
				Cursor = cursor,
				PageSize = pageSize,
				SearchTerm = search
			};

			var query = new GetClientsQuery(KeysetPaginationRequest);
			GetClientsResult result = await sender.Send(query, cancellationToken);
			var response = new GetClientsResponse(result.clients);
			return Results.Ok(response.clients);
		})
		.WithName("GetClients")
		.WithTags("Client Management")
		.Produces<KeysetPaginatedResult<ClientDetailsDTO>>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Clients")
		.WithDescription("Retrieves a paginated list of clients.")
		.RequireAuthorization();
	}
}
