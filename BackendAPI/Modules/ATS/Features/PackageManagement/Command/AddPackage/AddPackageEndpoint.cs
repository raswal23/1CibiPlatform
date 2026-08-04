namespace ATS.Features.PackageManagement.Command.AddPackage;

public record AddPackageRequest(AddPackageDTO package);

public record AddPackageResponse(bool isAdded);

public class AddPackageEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("addpackage", async (AddPackageRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddPackageCommand(request.package);
			AddPackageResult result = await sender.Send(command, cancellationToken);
			var response = new AddPackageResponse(result.isAdded);
			return Results.Ok(response.isAdded);
		})
		.WithName("AddPackage")
		.WithTags("ATS")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Add Package")
		.WithDescription("Add a new package.")
		.RequireAuthorization();
	}
}
