namespace ATS.Features.GetEmailIdAndApplicationFormPath;

public record GetEmailIdAndApplicationFormRequest(string HashToken);

public record GetEmailIdAndApplicationFormResponse(EmailIdAndApplicationFormPathDTO EmailIdAndApplicationFormPath);

public class GetEmailIdAndApplicationFormPathEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getemailidandapplicationformpath", async (string hashToken, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new GetEmailIdAndApplicationFormHandlerRequest(hashToken);
			GetEmailIdAndApplicationFormResult result = await sender.Send(command, cancellationToken);
			var response = new GetEmailIdAndApplicationFormResponse(result.EmailIdAndApplicationFormPath);
			return Results.Ok(response.EmailIdAndApplicationFormPath);
		})
		  .WithName("GetEmailIdAndApplicationFormPath")
		  .WithTags("ATS")
		  .Produces<GetEmailIdAndApplicationFormResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Get Email Id and Application Form Path")
		  .WithDescription("Get Email Id and Application Form Path");
	}
}
