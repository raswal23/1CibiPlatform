using System.Globalization;

namespace PlatformLogging.Services;

public sealed class PlatformLogService(IPlatformLogRepository repository) : IPlatformLogService
{
	public async Task<PlatformLogPageDTO> GetLogsAsync(DateTimeOffset? from, DateTimeOffset? to, string? application,
		string? level, string? search, string? cursor, int pageSize, CancellationToken cancellationToken)
	{
		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(cursor, 2);
		var (afterOccurredAt, afterId) = ParseCursorFields(fields);
		var clampedPageSize = KeysetPage.Clamp(pageSize);

		var rows = await repository.GetLogsPageAsync(
			from, 
			to, 
			application, 
			level, 
			search,
			clampedPageSize + 1, 
			afterOccurredAt, 
			afterId, 
			cancellationToken);
		var (items, hasMore) = KeysetPage.Trim(rows, clampedPageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(
				items[^1].OccurredAt.ToString("O"),
				items[^1].Id.ToString(CultureInfo.InvariantCulture))
			: null;

		return new PlatformLogPageDTO(items, nextCursor);
	}

	public Task<PlatformLogDTO?> GetLogByIdAsync(long id, CancellationToken cancellationToken)
		=> repository.GetLogByIdAsync(id, cancellationToken);

	private static (DateTimeOffset? Time, long? Id) ParseCursorFields(string[]? fields)
	{
		if (fields is null) return (null, null);
		return DateTimeOffset.TryParse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
			&& long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
			? (time, id) : (null, null);
	}
}
