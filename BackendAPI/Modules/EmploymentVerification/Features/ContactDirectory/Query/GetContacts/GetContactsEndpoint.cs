namespace EmploymentVerification.Features.ContactDirectory.Query.GetContacts;

public sealed record GetContactsEndpointRequest(
	string? Cursor = null,
	int? PageSize = 10,
	string? SearchTerm = null);

public sealed record GetContactsEndpointResponse(
	KeysetPaginatedResult<EmploymentVerificationContactDTO> Contacts);

public sealed class GetContactsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet(
				"api/employment-verification/contacts",
				async (
					[AsParameters] GetContactsEndpointRequest request,
					ISender sender,
					CancellationToken cancellationToken) =>
				{
					var query = new GetContactsQuery(
						request.Cursor,
						request.PageSize,
						request.SearchTerm);

					var result = await sender.Send(query, cancellationToken);

					return Results.Ok(new GetContactsEndpointResponse(result.Contacts));
				})
			.RequireAuthorization()
			.WithName("GetEmploymentVerificationContacts")
			.WithTags("Employment Verification")
			.Produces<GetContactsEndpointResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.WithSummary("Lists HR contact directory entries")
			.WithDescription(
				"Returns one keyset page of the HR contact directory ordered by company "
				+ "name, optionally filtered by a term matched against the company name "
				+ "and the email address. Inactive contacts are included so a row "
				+ "deactivated by mistake can still be found.");
	}
}
