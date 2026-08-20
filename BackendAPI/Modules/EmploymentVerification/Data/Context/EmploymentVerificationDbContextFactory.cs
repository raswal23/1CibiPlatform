namespace EmploymentVerification.Data.Context;

public sealed class EmploymentVerificationDbContextFactory
	: IDesignTimeDbContextFactory<EmploymentVerificationDbContext>
{
	public EmploymentVerificationDbContext CreateDbContext(string[] args)
	{
		var configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", optional: true)
			.AddJsonFile("appsettings.Development.json", optional: true)
			.AddEnvironmentVariables()
			.Build();

		var connection = configuration.GetConnectionString(
			"OnePlatform_Connection")
			?? "Host=localhost;Database=oneplatform;Username=postgres;Password=postgres";

		var options = new DbContextOptionsBuilder<EmploymentVerificationDbContext>()
			.UseNpgsql(connection, o => o.MigrationsAssembly("APIs"))
			.Options;

		return new EmploymentVerificationDbContext(options);
	}
}
