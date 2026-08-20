using PlatformLogging.Configuration;
using System.Linq.Expressions;

namespace PlatformLogging.Data.Repository;

public sealed class PlatformLogRepository(
	PlatformLoggingDBContext context,
	IOptions<PlatformLoggingOptions> options) : IPlatformLogRepository
{
	private readonly PlatformLoggingOptions _options = options.Value;
	public bool IsEnabled => _options.PostgreSqlEnabled;

	// Keyset over (OccurredAt DESC, Id DESC). Pure query — the service decodes the
	// cursor and mints the next one.
	public async Task<List<PlatformLogDTO>> GetLogsPageAsync(DateTimeOffset? from, DateTimeOffset? to,
		string? application, string? level, string? search, int take,
		DateTimeOffset? afterOccurredAt, long? afterId, CancellationToken cancellationToken)
	{
		if (!IsEnabled)
		{
			return [];
		}

		var query = context.LogEvents.AsNoTracking();

		if (from.HasValue)
		{
			query = query.Where(log => log.OccurredAt >= from.Value);
		}

		if (to.HasValue)
		{
			query = query.Where(log => log.OccurredAt <= to.Value);
		}

		if (!string.IsNullOrWhiteSpace(application))
		{
			query = query.Where(log => log.Application == application.Trim());
		}

		if (!string.IsNullOrWhiteSpace(level))
		{
			query = query.Where(log => log.Level == level.Trim());
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			var searchPattern = $"%{search.Trim()}%";
			query = query.Where(log =>
				EF.Functions.ILike(log.RenderedMessage, searchPattern));
		}

		if (afterOccurredAt.HasValue && afterId.HasValue)
		{
			query = query.Where(log =>
				log.OccurredAt < afterOccurredAt.Value
				|| log.OccurredAt == afterOccurredAt.Value && log.Id < afterId.Value);
		}

		return await query
			.OrderByDescending(log => log.OccurredAt)
			.ThenByDescending(log => log.Id)
			.Take(take)
			.Select(LogProjection)
			.ToListAsync(cancellationToken);
	}

	public Task<PlatformLogDTO?> GetLogByIdAsync(long id, CancellationToken cancellationToken)
	{
		return context.LogEvents
			.AsNoTracking()
			.Where(log => log.Id == id)
			.Select(LogProjection)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public Task<int> DeleteExpiredBatchAsync(CancellationToken cancellationToken)
	{
		if (!IsEnabled || !_options.RetentionEnabled)
		{
			return Task.FromResult(0);
		}

		var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _options.RetentionDays));
		var batchSize = Math.Max(100, _options.RetentionBatchSize);
		var expiredIds = context.LogEvents
			.Where(log => log.OccurredAt < cutoff)
			.OrderBy(log => log.OccurredAt)
			.Select(log => log.Id)
			.Take(batchSize);

		return context.LogEvents
			.Where(log => expiredIds.Contains(log.Id))
			.ExecuteDeleteAsync(cancellationToken);
	}

	private static readonly Expression<Func<PlatformLogEvent, PlatformLogDTO>> LogProjection =
		log => new PlatformLogDTO(
			log.Id,
			log.OccurredAt,
			log.Level,
			log.RenderedMessage,
			log.Exception,
			log.Platform,
			log.Application,
			log.Environment,
			log.SourceContext,
			log.TraceId,
			log.RequestId,
			log.Properties);
}
