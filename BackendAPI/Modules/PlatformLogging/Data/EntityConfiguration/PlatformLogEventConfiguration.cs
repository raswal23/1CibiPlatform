namespace PlatformLogging.Data.EntityConfiguration;

public sealed class PlatformLogEventConfiguration : IEntityTypeConfiguration<PlatformLogEvent>
{
	public void Configure(EntityTypeBuilder<PlatformLogEvent> builder)
	{
		builder.ToTable("log_events", "logging");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
		builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
		builder.Property(x => x.Level).HasColumnName("level").HasMaxLength(32).IsRequired();
		builder.Property(x => x.MessageTemplate).HasColumnName("message_template");
		builder.Property(x => x.RenderedMessage).HasColumnName("rendered_message").IsRequired();
		builder.Property(x => x.Exception).HasColumnName("exception");
		builder.Property(x => x.Properties)
			.HasColumnName("properties")
			.HasColumnType("jsonb")
			.HasDefaultValueSql("jsonb_build_object()")
			.IsRequired();
		builder.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(100).IsRequired();
		builder.Property(x => x.Application).HasColumnName("application").HasMaxLength(100).IsRequired();
		builder.Property(x => x.Environment).HasColumnName("environment").HasMaxLength(50).IsRequired();
		builder.Property(x => x.SourceContext).HasColumnName("source_context");
		builder.Property(x => x.TraceId).HasColumnName("trace_id").HasMaxLength(64);
		builder.Property(x => x.RequestId).HasColumnName("request_id").HasMaxLength(100);
		builder.HasIndex(x => new { x.OccurredAt, x.Id }).IsDescending();
		builder.HasIndex(x => new { x.Application, x.OccurredAt }).IsDescending(false, true);
		builder.HasIndex(x => new { x.Level, x.OccurredAt }).IsDescending(false, true);
		builder.HasIndex(x => new { x.TraceId, x.OccurredAt }).IsDescending(false, true).HasFilter("trace_id IS NOT NULL");
	}
}
