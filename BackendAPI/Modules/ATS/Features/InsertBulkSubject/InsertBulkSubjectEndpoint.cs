namespace ATS.Features.InsertBulkSubject;

public record InsertBulkSubjectRequest(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO);
public record InsertBulkSubjectResponse(Guid FiledID);

public class InsertBulkSubjectEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("insertbulksubject", async (InsertBulkSubjectRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new InsertBulkSubjectCommand(request.bulkUploadFileDetailsDTO);
			InsertBulkSubjectResult result = await sender.Send(command, cancellationToken);
			var response = new InsertBulkSubjectResponse(result.FiledID);
			return Results.Json(response);
		})
		  .WithName("InsertBulkSubject")
		  .WithTags("ATS")
		  .Produces<InsertBulkSubjectResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Insert Bulk Subject")
		  .WithDescription("Uploads a file and inserts its metadata to the database in a transactional manner.");
	}
}
