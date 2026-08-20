namespace APIs.Data;

using Auth.Data.Context;

public class AuthApplicationDbContextFactory : IDesignTimeDbContextFactory<AuthApplicationDbContext>
{
	public AuthApplicationDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OnePlatform_Connection")
			?? "Host=localhost;Database=OnePlatform;Username=postgres";

		var optionsBuilder = new DbContextOptionsBuilder<AuthApplicationDbContext>();
		optionsBuilder.UseNpgsql(
			connectionString,
			npgsqlOptions => npgsqlOptions.MigrationsAssembly("APIs"));

		return new AuthApplicationDbContext(optionsBuilder.Options);
	}
}
