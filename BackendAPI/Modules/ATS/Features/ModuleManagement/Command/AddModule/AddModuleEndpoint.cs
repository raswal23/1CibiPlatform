namespace ATS.Features.ModuleManagement.Command.AddModule;

public record AddModuleRequest(AddModuleDTO module);

public record AddModuleResponse(bool isAdded);

public class AddModuleEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("addmodule", async (AddModuleRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddModuleCommand(request.module);
			AddModuleResult result = await sender.Send(command, cancellationToken);
			var response = new AddModuleResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("ATSAddModule")
		.WithTags("Module Management")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add Module")
		.WithDescription("Add a new ATS module.")
		.RequireAuthorization();
	}
}
