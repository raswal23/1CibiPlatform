namespace ATS.Features.Web.ModuleManagement.Command.EditModule;

public record EditModuleRequest(EditModuleDTO editModule);

public record EditModuleResponse(ModuleDetailsDTO module);

public class EditModuleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("editmodule", async (EditModuleRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditModuleCommand(request.editModule);
			EditModuleResult result = await sender.Send(command, cancellationToken);
			var response = new EditModuleResponse(result.module);
			return Results.Ok(response.module);
		})
		.WithName("ATSEditModule")
		.WithTags("Module Management")
		.Produces<ModuleDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit Module")
		.WithDescription("Edits an existing ATS module.")
		.RequireAuthorization();
	}
}
