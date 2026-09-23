namespace EmploymentVerification.Features.ContactDirectory.Command.EditContact;

public sealed record EditContactEndpointRequest(
	EditEmploymentVerificationContactDTO Contact);

public sealed record EditContactEndpointResponse(bool IsUpdated);

public sealed class EditContactEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch(
				"api/employment-verification/contacts",
				async (
					EditContactEndpointRequest request,
					ISender sender,
					CancellationToken cancellationToken) =>
				{
					var command = new EditContactCommand(request.Contact);
					var result = await sender.Send(command, cancellationToken);

					return Results.Ok(new EditContactEndpointResponse(result.IsUpdated));
				})
			.RequireAuthorization()
			.WithName("EditEmploymentVerificationContact")
			.WithTags("Employment Verification")
			.Produces<EditContactEndpointResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.WithSummary("Updates an HR contact directory entry")
			.WithDescription(
				"Updates a contact's company, mailbox or active flag. Deactivating is "
				+ "the only form of removal - there is no delete endpoint, matching ATS "
				+ "module and client management. Returns 409 when the edit would "
				+ "duplicate another entry for the same company.");
	}
}
