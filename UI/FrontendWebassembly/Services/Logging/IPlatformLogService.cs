using FrontendWebassembly.DTO.Logging;

namespace FrontendWebassembly.Services.Logging;

public interface IPlatformLogService
{
	Task<PlatformLogPageDTO> GetLogsAsync(DateTimeOffset? from, DateTimeOffset? to, string? application,
		string? level, string? search, string? cursor, int pageSize, CancellationToken cancellationToken = default);
	Task<PlatformLogDTO?> GetLogAsync(long id, CancellationToken cancellationToken = default);
}
