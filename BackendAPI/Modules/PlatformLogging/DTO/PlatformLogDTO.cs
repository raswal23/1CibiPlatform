namespace PlatformLogging.DTO;

public sealed record PlatformLogDTO(
	long Id, DateTimeOffset OccurredAt, string Level, string RenderedMessage,
	string? Exception, string Platform, string Application, string Environment,
	string? SourceContext, string? TraceId, string? RequestId, string Properties);

public sealed record PlatformLogPageDTO(IReadOnlyList<PlatformLogDTO> Items, string? NextCursor);
