namespace ATS.Features.Web.EmailProcessManagement.Query.GetEmailProcesses;

public record GetEmailProcessesResponse(IReadOnlyList<EmailProcessDetailsDTO> emailProcesses);

public class GetEmailProcessesEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getemailprocesses", async (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(new GetEmailProcessesQuery(), cancellationToken);
			var response = new GetEmailProcessesResponse(result.emailProcesses);

			return Results.Ok(response);
		})
		.WithName("GetEmailProcesses")
		.WithTags("Email Process Management")
		.Produces<GetEmailProcessesResponse>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Email Processes")
		.WithDescription("Every ATS notice copy list, active and inactive, ordered by process name.")
		.RequireAuthorization()
		.RequireActiveAtsUser();
	}
}
