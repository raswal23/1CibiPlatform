namespace ATS.Features.PackageManagement.Command.EditPackage;

public record EditPackageRequest(EditPackageDTO editPackage);

public record EditPackageResponse(PackageDetailsDTO package);

public class EditPackageEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("editpackage", async (EditPackageRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new EditPackageCommand(request.editPackage);
			EditPackageResult result = await sender.Send(command, cancellationToken);
			var response = new EditPackageResponse(result.package);
			return Results.Ok(response.package);
		})
		.WithName("EditPackage")
		.WithTags("ATS")
		.Produces<PackageDetailsDTO>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Edit Package")
		.WithDescription("Edits an existing package.")
		.RequireAuthorization();
	}
}
