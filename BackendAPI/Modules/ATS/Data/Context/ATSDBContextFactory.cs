namespace ATS.Data.Context;

public class ATSDBContextFactory : IDesignTimeDbContextFactory<ATSDBContext>
{
	public ATSDBContext CreateDbContext(string[] args)
	{
		var basePath = Directory.GetCurrentDirectory();

		var envPath = FindEnvFile(basePath);
		if (!string.IsNullOrEmpty(envPath))
		{
			foreach (var line in File.ReadAllLines(envPath))
			{
				var trimmed = line.Trim();
				if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
					continue;

				var idx = trimmed.IndexOf('=');
				if (idx <= 0) continue;

				var key = trimmed.Substring(0, idx).Trim();
				var value = trimmed.Substring(idx + 1).Trim().Trim('"');
				Environment.SetEnvironmentVariable(key, value);
			}
		}

		var config = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.Build();

		var connectionString = config.GetConnectionString("OnePlatform_Connection")
							   ?? Environment.GetEnvironmentVariable("OnePlatform_Connection");

		if(string.IsNullOrEmpty(connectionString))
		{
			throw new NotFoundException("Connection string 'OnePlatform_Connection' not found in environment variables or configuration.");
		}

		var optionsBuilder = new DbContextOptionsBuilder<ATSDBContext>();
		optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("APIs"));

		return new ATSDBContext(optionsBuilder.Options);
	}

	private static string FindEnvFile(string startDirectory)
	{
		var dir = new DirectoryInfo(startDirectory);
		for (int i = 0; i < 6 && dir != null; i++)
		{
			var candidate = System.IO.Path.Combine(dir.FullName, ".env");
			if (File.Exists(candidate)) return candidate;
			dir = dir.Parent;
		}
		return null;
	}
}
