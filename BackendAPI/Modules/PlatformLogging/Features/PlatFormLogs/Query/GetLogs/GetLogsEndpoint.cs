namespace PlatformLogging.Features.Logs.Query.GetLogs;

public sealed class GetLogsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("platform-logging/logs", async (
			DateTimeOffset? from, 
			DateTimeOffset? to,
			string? application, 
			string? level, string? 
			search, string? cursor, 
			int pageSize = 50,
			ISender sender = null!, 
			CancellationToken cancellationToken = default) =>
		{
			var result = await sender.Send(new GetLogsQuery(
				from, 
				to,
				application, 
				level, 
				search, 
				cursor, 
				pageSize), 
				cancellationToken);
			return Results.Ok(result.Logs);
		})
		.WithName("GetPlatformLogs").WithTags("Platform Logging")
		.Produces<PlatformLogPageDTO>().ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get platform logs").RequireAuthorization();
	}
}
