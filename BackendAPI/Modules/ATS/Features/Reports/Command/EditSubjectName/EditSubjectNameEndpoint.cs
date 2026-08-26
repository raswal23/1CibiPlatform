namespace ATS.Features.Reports.Command.EditSubjectName;

public record EditSubjectNameRequest(EditSubjectNameDTO editSubjectName);

public record EditSubjectNameResponse(SubjectNameDTO subject);

public class EditSubjectNameEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("editsubjectname", async (
			EditSubjectNameRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new EditSubjectNameCommand(request.editSubjectName);

			EditSubjectNameResult result = await sender.Send(command, cancellationToken);

			var response = new EditSubjectNameResponse(result.subject);

			return Results.Ok(response);
		})
		.WithName("EditSubjectName")
		.WithTags("ATS")
		.Produces<EditSubjectNameResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status403Forbidden)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Edit Subject Name")
		.WithDescription("Corrects the first, middle, and last name of an order's subject.")
		.RequireAuthorization();
	}
}
