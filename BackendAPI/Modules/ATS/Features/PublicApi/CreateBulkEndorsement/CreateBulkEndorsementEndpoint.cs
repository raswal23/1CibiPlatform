namespace ATS.Features.PublicApi.CreateBulkEndorsement;

public record CreateBulkEndorsementEndpointRequest(
	IFormFile File,
	string Package,
	string OrderType);

public record CreateBulkEndorsementEndpointResponse(Guid FileId, bool Accepted);

public class CreateBulkEndorsementEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("api/public/ats/endorsements/bulk", async (
			[FromForm] CreateBulkEndorsementEndpointRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new CreateBulkEndorsementCommand(
				request.File,
				request.Package,
				request.OrderType);

			var result = await sender.Send(command, cancellationToken);

			// 202: the file is queued here and parsed by a background job within
			// seconds. Per-row results are read back from the status endpoint below.
			return Results.Accepted(
				$"/api/public/ats/endorsements/bulk/{result.FileId}",
				new CreateBulkEndorsementEndpointResponse(result.FileId, result.Accepted));
		})
		.DisableAntiforgery()
		.WithName("PublicCreateBulkEndorsement")
		.WithTags("ATS Public API")
		.Produces<CreateBulkEndorsementEndpointResponse>(StatusCodes.Status202Accepted)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.WithSummary("Create endorsements from a CSV file")
		.WithDescription(
			"Uploads a CSV of subjects and queues it for processing. Required columns: "
			+ "LastName, FirstName, MiddleInitial, EmailAddress, MobileNumber. "
			+ "MiddleInitial may be blank. Rows that fail validation are skipped and "
			+ "reported by the status endpoint rather than failing the whole file.")
		.RequireAuthorization();
	}
}
