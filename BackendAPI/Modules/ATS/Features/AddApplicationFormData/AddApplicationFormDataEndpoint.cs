using MediatR;
using Microsoft.AspNetCore.Http;

namespace ATS.Features.AddApplicationFormData;

public record AddApplicationFormDataRequest(ApplicationFormDataDTO ApplicationFormDataDTO);
public record AddApplicationFormDataResponse(bool IsAdded);

public class AddApplicationFormDataEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost("addapplicationform", async (AddApplicationFormDataRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddApplicationFormDataCommand(request.ApplicationFormDataDTO);
			AddApplicationFormDataResult result = await sender.Send(command, cancellationToken);
			var response = new AddApplicationFormDataResponse(result.IsAdded);
			return Results.Json(response);
		})
		  .WithName("AddApplicationFormData")
		  .WithTags("ATS")
		  .Produces<AddApplicationFormDataResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Add Application Form Data")
		  .WithDescription("Adds a new application form data entry to the database.");
	}
}
