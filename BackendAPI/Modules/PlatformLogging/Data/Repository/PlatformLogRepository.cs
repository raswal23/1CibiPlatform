using PlatformLogging.Configuration;
using System.Linq.Expressions;

namespace PlatformLogging.Data.Repository;

public sealed class PlatformLogRepository(
	PlatformLoggingDBContext context,
	IOptions<PlatformLoggingOptions> options) : IPlatformLogRepository
{
	private readonly PlatformLoggingOptions _options = options.Value;
	public bool IsEnabled => _options.PostgreSqlEnabled;

	public async Task<PlatformLogPageDTO> GetLogsAsync(DateTimeOffset? from, DateTimeOffset? to,
		string? application, string? level, string? search, int pageSize,
		DateTimeOffset? cursorTime, long? cursorId, CancellationToken cancellationToken)
	{
		if (!IsEnabled)
		{
			return new PlatformLogPageDTO([], null);
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

		if (cursorTime.HasValue && cursorId.HasValue)
		{
			query = query.Where(log =>
				log.OccurredAt < cursorTime.Value
				|| log.OccurredAt == cursorTime.Value && log.Id < cursorId.Value);
		}

		var items = await query
			.OrderByDescending(log => log.OccurredAt)
			.ThenByDescending(log => log.Id)
			.Take(pageSize + 1)
			.Select(LogProjection)
			.ToListAsync(cancellationToken);

		string? nextCursor = null;
		if (items.Count > pageSize)
		{
			items.RemoveAt(items.Count - 1);
			var last = items[^1];
			var cursorValue = $"{last.OccurredAt:O}|{last.Id}";
			nextCursor = Convert.ToBase64String(
				Encoding.UTF8.GetBytes(cursorValue));
		}

		return new PlatformLogPageDTO(items, nextCursor);
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
