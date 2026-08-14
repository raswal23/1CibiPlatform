using PlatformLogging.Data.Context;

namespace APIs.Data;

public sealed class PlatformLoggingDBContextFactory : IDesignTimeDbContextFactory<PlatformLoggingDBContext>
{
	public PlatformLoggingDBContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OnePlatform_Connection")
			?? "Host=localhost;Database=OnePlatform;Username=postgres";
		var optionsBuilder = new DbContextOptionsBuilder<PlatformLoggingDBContext>();
		optionsBuilder.UseNpgsql(connectionString, options => options.MigrationsAssembly("APIs"));
		return new PlatformLoggingDBContext(optionsBuilder.Options);
	}
}
