namespace PlatformLogging.Data.Repository;

public interface IPlatformLogRepository
{
	bool IsEnabled { get; }
	Task<List<PlatformLogDTO>> GetLogsPageAsync(DateTimeOffset? from, DateTimeOffset? to, string? application,
		string? level, string? search, int take, DateTimeOffset? afterOccurredAt, long? afterId,
		CancellationToken cancellationToken);
	Task<PlatformLogDTO?> GetLogByIdAsync(long id, CancellationToken cancellationToken);
	Task<int> DeleteExpiredBatchAsync(CancellationToken cancellationToken);
}
