namespace PlatformLogging.Features.Logs.Query.GetLogById;

public sealed class GetLogByIdEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("platform-logging/logs/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(new GetLogByIdQuery(id), cancellationToken);
			return result.Log is null ? Results.NotFound() : Results.Ok(result.Log);
		}).WithName("GetPlatformLogById")
		  .WithTags("Platform Logging")
		  .Produces<PlatformLogDTO>()
		  .Produces(StatusCodes.Status404NotFound).RequireAuthorization();
	}
}
