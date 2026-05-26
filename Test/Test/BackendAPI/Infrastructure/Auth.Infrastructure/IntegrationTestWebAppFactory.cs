using Auth.Data.Context;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Test.BackendAPI.Infrastructure.Auth.Infrastructure;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	private readonly PostgreSqlContainer _dbContainer;

	public IntegrationTestWebAppFactory()
	{
		_dbContainer = new PostgreSqlBuilder()
			.WithDatabase("test_db")
			.WithUsername("postgres")
			.WithPassword("p@ssw0rd!")
			.WithImage("postgres:16")
			.Build();
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
      builder.UseEnvironment("Testing");

		// Load environment variables from a .env file if present (allows CI/local test overrides)
		LoadDotEnv();

		// Set environment variables for test configuration
		Environment.SetEnvironmentVariable("OpenAI__Endpoint", "https://test.openai.com");
		Environment.SetEnvironmentVariable("OpenAI__ApiKey", "test-api-key");
		Environment.SetEnvironmentVariable("OpenAI__Model", "gpt-4");
		Environment.SetEnvironmentVariable("OpenAI__EmbeddingModel", "text-embedding-3-small");

		builder.ConfigureTestServices(services =>
		{
			// Remove existing DbContext registration
			var descriptor = services
				.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<AuthApplicationDbContext>));

			if (descriptor is not null)
				services.Remove(descriptor);

			// Register test DB context
			services.AddDbContext<AuthApplicationDbContext>(options =>
				options.UseNpgsql(_dbContainer.GetConnectionString()));

			// Register HttpContextAccessor (scoped, not singleton)
			services.RemoveAll<IHttpContextAccessor>();
			services.AddScoped<IHttpContextAccessor>(_ =>
			{
				var fakeHttpContext = new DefaultHttpContext();
				fakeHttpContext.Response.Body = new MemoryStream();
				return new HttpContextAccessor { HttpContext = fakeHttpContext };
			});

			// Replace email sender
			services.RemoveAll<IEmailService>();
			services.AddSingleton<IEmailService, FakeEmailSender>();
		});
	}

	private static void LoadDotEnv()
	{
		var startDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
		var dir = new DirectoryInfo(startDir);
		while (dir != null)
		{
			var envPath = Path.Combine(dir.FullName, ".env");
			if (File.Exists(envPath))
			{
				foreach (var rawLine in File.ReadAllLines(envPath))
				{
					var line = rawLine?.Trim();
					if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
						continue;
					var idx = line.IndexOf('=');
					if (idx <= 0)
						continue;
					var key = line.Substring(0, idx).Trim();
					var val = line.Substring(idx + 1).Trim().Trim('"');
					if (!string.IsNullOrEmpty(key))
						Environment.SetEnvironmentVariable(key, val);
				}
				break;
			}
			dir = dir.Parent;
		}
	}

	public async Task InitializeAsync()
	{
		await _dbContainer.StartAsync();

		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AuthApplicationDbContext>();
		await db.Database.MigrateAsync();
	}

	public async Task DisposeAsync()
	{
		await _dbContainer.StopAsync();
	}
}
