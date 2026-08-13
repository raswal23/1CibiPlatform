namespace PlatformLogging.Configuration;

public sealed class PlatformLoggingOptions
{
	public const string SectionName = "PlatformLogging";
	public bool PostgreSqlEnabled { get; set; }
	public string ConnectionStringName { get; set; } = "OnePlatform_Connection";
	public string Schema { get; set; } = "logging";
	public string Table { get; set; } = "log_events";
	public int BufferSize { get; set; } = 10_000;
	public int BatchSize { get; set; } = 100;
	public int FlushIntervalSeconds { get; set; } = 2;
	public bool RetentionEnabled { get; set; } = true;
	public int RetentionDays { get; set; } = 30;
	public int RetentionIntervalHours { get; set; } = 24;
	public int RetentionBatchSize { get; set; } = 5_000;
}
