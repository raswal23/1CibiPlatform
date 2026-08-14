namespace PlatformLogging.Data.Repository;

public interface IPlatformLogRepository
{
	bool IsEnabled { get; }
	Task<PlatformLogPageDTO> GetLogsAsync(DateTimeOffset? from, DateTimeOffset? to, string? application,
		string? level, string? search, int pageSize, DateTimeOffset? cursorTime, long? cursorId,
		CancellationToken cancellationToken);
	Task<PlatformLogDTO?> GetLogByIdAsync(long id, CancellationToken cancellationToken);
	Task<int> DeleteExpiredBatchAsync(CancellationToken cancellationToken);
}
