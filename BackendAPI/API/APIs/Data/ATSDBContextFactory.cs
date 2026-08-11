namespace APIs.Data;

using ATS.Data.Context;

public class ATSDBContextFactory : IDesignTimeDbContextFactory<ATSDBContext>
{
	public ATSDBContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OnePlatform_Connection")
			?? "Host=localhost;Database=OnePlatform;Username=postgres";

		var optionsBuilder = new DbContextOptionsBuilder<ATSDBContext>();
		optionsBuilder.UseNpgsql(
			connectionString,
			npgsqlOptions => npgsqlOptions.MigrationsAssembly("APIs"));

		return new ATSDBContext(optionsBuilder.Options);
	}
}
