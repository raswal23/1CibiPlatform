namespace PlatformLogging.Data.Context;

public sealed class PlatformLoggingDBContext(DbContextOptions<PlatformLoggingDBContext> options) : DbContext(options)
{
	public DbSet<PlatformLogEvent> LogEvents { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformLoggingDBContext).Assembly);
		base.OnModelCreating(modelBuilder);
	}
}
