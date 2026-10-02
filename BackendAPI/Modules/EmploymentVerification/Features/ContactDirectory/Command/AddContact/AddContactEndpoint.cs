namespace EmploymentVerification.Features.ContactDirectory.Command.AddContact;

public sealed record AddContactEndpointRequest(
	AddEmploymentVerificationContactDTO Contact);

public sealed record AddContactEndpointResponse(bool IsAdded);

public sealed class AddContactEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost(
				"api/employment-verification/contacts",
				async (
					AddContactEndpointRequest request,
					ISender sender,
					CancellationToken cancellationToken) =>
				{
					var command = new AddContactCommand(request.Contact);
					var result = await sender.Send(command, cancellationToken);

					return Results.Ok(new AddContactEndpointResponse(result.IsAdded));
				})
			.RequireAuthorization()
			.WithName("AddEmploymentVerificationContact")
			.WithTags("Employment Verification")
			.Produces<AddContactEndpointResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.WithSummary("Adds an HR contact directory entry")
			.WithDescription(
				"Adds a company/mailbox pair to the HR contact directory. Returns 409 "
				+ "when that mailbox is already listed for the same company; the same "
				+ "mailbox under a different company is allowed, because a shared HR "
				+ "inbox serving several subsidiaries is legitimate.");
	}
}
