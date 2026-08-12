namespace PlatformLogging.Services;

public interface IPlatformLogService
{
	Task<PlatformLogPageDTO> GetLogsAsync(DateTimeOffset? from, DateTimeOffset? to, string? application,
		string? level, string? search, string? cursor, int pageSize, CancellationToken cancellationToken);
	Task<PlatformLogDTO?> GetLogByIdAsync(long id, CancellationToken cancellationToken);
}
