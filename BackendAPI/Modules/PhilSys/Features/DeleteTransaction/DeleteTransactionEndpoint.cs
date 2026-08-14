namespace PhilSys.Features.DeleteTransaction;
public record DeleteTransactionRequest(string HashToken);
public record DeleteTransactionResponse(bool IsDeleted);
public class DeleteTransactionEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapDelete("deletetransaction/{HashToken}", async (string HashToken, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new DeleteTransactionCommand(HashToken);
			DeleteTransactionResult result = await sender.Send(command, cancellationToken);
			var response = new DeleteTransactionResponse(result.IsDeleted);
			return Results.Ok(response.IsDeleted);
		})
		.WithName("DeleteTransaction")
		.WithTags("PhilSys")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Delete Transaction")
		.WithDescription("Deletes an existing PhilSys transaction based on the provided unique hash token.");
	}
}
