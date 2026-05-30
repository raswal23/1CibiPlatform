using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhilSys.Data.Context;
using System.Net;
using System.Text;
using Testcontainers.PostgreSql;

namespace Test.BackendAPI.Infrastructure.PhilSys.Infrastracture;

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

		builder.ConfigureTestServices(services =>
		{
			// Remove existing DbContext registration
			var descriptor = services
				.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<PhilSysDBContext>));

			if (descriptor is not null)
				services.Remove(descriptor);

			// Register test DB context
			services.AddDbContext<PhilSysDBContext>(options =>
				options.UseNpgsql(_dbContainer.GetConnectionString()));

			// Register HttpContextAccessor (scoped, not singleton)
			services.RemoveAll<IHttpContextAccessor>();
			services.AddScoped<IHttpContextAccessor>(_ =>
			{
				var fakeHttpContext = new DefaultHttpContext();
				fakeHttpContext.Response.Body = new MemoryStream();
				return new HttpContextAccessor { HttpContext = fakeHttpContext };
			});


			services.AddTransient<PhilSysTestHandler>(_ => new PhilSysTestHandler((req, ct) =>
			{
				// default: return BadRequest with null data
				var resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
				{
					Content = new StringContent("{\"data\": null}", Encoding.UTF8, "application/json")
				};
				return Task.FromResult(resp);
			}));

			services.AddHttpClient("PhilSys")
				.ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<PhilSysTestHandler>());
		});
	}

	public WebApplicationFactory<Program> CreateCustomFactory(Action<IServiceCollection> configureTestServices)
	{
		return this.WithWebHostBuilder(builder =>
		{
			builder.ConfigureTestServices(services =>
			{
				configureTestServices?.Invoke(services);
			});
		});
	}
	public WebApplicationFactory<Program> CreateFactoryWithHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
	=> CreateCustomFactory(services =>
	{
		services.AddTransient<PhilSysTestHandler>(_ => new PhilSysTestHandler(responder));
	});

	public async Task InitializeAsync()
	{
		await _dbContainer.StartAsync();

		using var scope = Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<PhilSysDBContext>();
		await db.Database.MigrateAsync();
	}

	public async Task DisposeAsync()
	{
		await _dbContainer.StopAsync();
	}
}
