namespace ATS.Features.Web.AIAssistant.Query.SearchOrdersBySubject;

public record SearchOrdersBySubjectEndpointRequest(string Name);

public record SearchOrdersBySubjectEndpointResponse(IReadOnlyList<AtsOrderSummaryDTO> Orders);

public class SearchOrdersBySubjectEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("searchordersbysubject", async (
			[AsParameters] SearchOrdersBySubjectEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new SearchOrdersBySubjectQueryRequest(request.Name);

			var result = await sender.Send(query, cancellationToken);

			return Results.Ok(new SearchOrdersBySubjectEndpointResponse(result.Orders));
		})
		.WithName("SearchOrdersBySubject")
		.WithTags("ATS")
		.Produces<SearchOrdersBySubjectEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Search orders by subject name")
		.WithDescription("Retrieves the orders whose candidate name matches the search term, "
			+ "scoped to the current user's ATS access.")
		.RequireAuthorization();
	}
}
