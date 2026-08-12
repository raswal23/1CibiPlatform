namespace PlatformLogging.Data.Entities;

public sealed class PlatformLogEvent
{
	public long Id { get; set; }
	public DateTimeOffset OccurredAt { get; set; }
	public string Level { get; set; } = string.Empty;
	public string? MessageTemplate { get; set; }
	public string RenderedMessage { get; set; } = string.Empty;
	public string? Exception { get; set; }
	public string Properties { get; set; } = "{}";
	public string Platform { get; set; } = string.Empty;
	public string Application { get; set; } = string.Empty;
	public string Environment { get; set; } = string.Empty;
	public string? SourceContext { get; set; }
	public string? TraceId { get; set; }
	public string? RequestId { get; set; }
}
