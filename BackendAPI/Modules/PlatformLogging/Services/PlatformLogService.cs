namespace PlatformLogging.Services;

public sealed class PlatformLogService(IPlatformLogRepository repository) : IPlatformLogService
{
	public Task<PlatformLogPageDTO> GetLogsAsync(DateTimeOffset? from, DateTimeOffset? to, string? application,
		string? level, string? search, string? cursor, int pageSize, CancellationToken cancellationToken)
	{
		var parsedCursor = ParseCursor(cursor);
		return repository.GetLogsAsync(from, to, application, level, search, Math.Clamp(pageSize, 1, 100),
			parsedCursor.Time, parsedCursor.Id, cancellationToken);
	}

	public Task<PlatformLogDTO?> GetLogByIdAsync(long id, CancellationToken cancellationToken)
		=> repository.GetLogByIdAsync(id, cancellationToken);

	private static (DateTimeOffset? Time, long? Id) ParseCursor(string? cursor)
	{
		if (string.IsNullOrWhiteSpace(cursor)) return (null, null);
		try
		{
			var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
			return parts.Length == 2 && DateTimeOffset.TryParse(parts[0], out var time) && long.TryParse(parts[1], out var id)
				? (time, id) : (null, null);
		}
		catch (FormatException) { return (null, null); }
	}
}
