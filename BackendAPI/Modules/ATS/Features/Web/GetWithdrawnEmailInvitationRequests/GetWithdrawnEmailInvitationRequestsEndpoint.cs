namespace ATS.Features.Web.EmailInvitationRequest;

public record GetWithdrawnEmailInvitationRequestsEndpointRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null);

public record GetWithdrawnEmailInvitationRequestsEndpointResponse(KeysetPaginatedResult<EmailInvitationRequestListDTO> Requests);

public class GetWithdrawnEmailInvitationRequestsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getwithdrawnapplicationforms", async (
			[AsParameters] GetWithdrawnEmailInvitationRequestsEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetWithdrawnEmailInvitationRequestsQueryRequest(
				request.Cursor,
				request.PageSize,
				request.SearchTerm);

			var result = await sender.Send(query, cancellationToken);

			var response = new GetWithdrawnEmailInvitationRequestsEndpointResponse(result.Requests);

			return Results.Ok(response);
		})
		.WithName("GetWithdrawnEmailInvitationRequests")
		.WithTags("ATS")
		.Produces<GetWithdrawnEmailInvitationRequestsEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Withdrawn Email Invitation Requests")
		.WithDescription("Retrieves a paginated list of email invitation requests with OrderStatus = 'Application Withdrawn'.")
		.RequireAuthorization();
	}
}
