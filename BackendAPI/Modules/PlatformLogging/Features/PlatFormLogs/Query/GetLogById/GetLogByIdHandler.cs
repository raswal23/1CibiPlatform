namespace PlatformLogging.Features.Logs.Query.GetLogById;

public record GetLogByIdQuery(long Id) : IQuery<GetLogByIdResult>;
public record GetLogByIdResult(PlatformLogDTO? Log);
public sealed class GetLogByIdHandler(IPlatformLogService service) : IQueryHandler<GetLogByIdQuery, GetLogByIdResult>
{
	public async Task<GetLogByIdResult> Handle(GetLogByIdQuery request, CancellationToken cancellationToken)
		=> new(await service.GetLogByIdAsync(request.Id, cancellationToken));
}
