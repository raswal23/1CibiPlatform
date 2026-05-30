using ATS.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Test.BackendAPI.Infrastructure.ATS.Infrastracture;
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

		var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
		DotEnvLoader.Load(Path.Combine(solutionRoot, ".env"));

		// Set environment variables for test configuration
		Environment.SetEnvironmentVariable("OpenAI__Endpoint", "https://test.openai.com");
		Environment.SetEnvironmentVariable("OpenAI__ApiKey", "test-api-key");
		Environment.SetEnvironmentVariable("OpenAI__Model", "gpt-4");
		Environment.SetEnvironmentVariable("OpenAI__EmbeddingModel", "text-embedding-3-small");

		builder.ConfigureServices(services =>
		{
			// Remove existing DbContext registration
			var descriptor = services
				.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<ATSDBContext>));

			if (descriptor is not null)
				services.Remove(descriptor);

			// Register test DB context
			services.AddDbContext<ATSDBContext>(options =>
				options.UseNpgsql(_dbContainer.GetConnectionString()));

			// Register HttpContextAccessor (scoped, not singleton)
			services.RemoveAll<IHttpContextAccessor>();
			services.AddScoped<IHttpContextAccessor>(_ =>
			{
				var fakeHttpContext = new DefaultHttpContext();
				fakeHttpContext.Response.Body = new MemoryStream();
				return new HttpContextAccessor { HttpContext = fakeHttpContext };
			});
		});

		builder.ConfigureAppConfiguration((ctx, cb) =>
		{
			cb.AddInMemoryCollection(new[]
			{
				new KeyValuePair<string,string>("AlibabaOss:TestPrefix", $"uploads/tests/{Guid.NewGuid():N}/")
			});
		});
	}

	public async Task InitializeAsync()
	{
		await _dbContainer.StartAsync();

		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ATSDBContext>();
		await db.Database.MigrateAsync();
	}

	public async Task DisposeAsync()
	{
		await _dbContainer.StopAsync();
	}
}
