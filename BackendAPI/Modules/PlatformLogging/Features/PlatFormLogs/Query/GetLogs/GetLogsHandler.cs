namespace PlatformLogging.Features.Logs.Query.GetLogs;

public record GetLogsQuery(DateTimeOffset? From, DateTimeOffset? To, string? Application,
	string? Level, string? Search, string? Cursor, int PageSize) : IQuery<GetLogsResult>;

public sealed class GetLogsQueryValidator : AbstractValidator<GetLogsQuery>
{
	public GetLogsQueryValidator()
	{
		RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
		RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.To >= x.From)
			.WithMessage("The 'to' date must be after 'from'.");
		RuleFor(x => x).Must(x => !x.From.HasValue || !x.To.HasValue || x.To.Value - x.From.Value <= TimeSpan.FromDays(31))
			.WithMessage("Date ranges are limited to 31 days.");
	}
}

public record GetLogsResult(PlatformLogPageDTO Logs);

public sealed class GetLogsHandler(IPlatformLogService service) : IQueryHandler<GetLogsQuery, GetLogsResult>
{
	public async Task<GetLogsResult> Handle(GetLogsQuery request, CancellationToken cancellationToken)
		=> new(await service.GetLogsAsync(
			request.From, 
			request.To, 
			request.Application, 
			request.Level,
			request.Search, 
			request.Cursor, 
			request.PageSize, 
			cancellationToken));
}
