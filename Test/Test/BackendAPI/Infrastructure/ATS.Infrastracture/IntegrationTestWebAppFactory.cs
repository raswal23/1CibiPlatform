using ATS.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Auth.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Quartz;
using System.Security.Claims;
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

		builder.ConfigureServices(services =>
		{
			// Remove hosted services to avoid affecting other tests
			var quartzHostedServices = services
				.Where(s =>
					s.ServiceType == typeof(IHostedService) &&
					s.ImplementationType == typeof(QuartzHostedService))
				.ToList();

			foreach (var service in quartzHostedServices)
			{
				services.Remove(service);
			}

			services.RemoveAll<IDistributedCache>();

			services.AddDistributedMemoryCache();

			// Remove existing DbContext registration
			var descriptor = services
				.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<ATSDBContext>));

			if (descriptor is not null)
				services.Remove(descriptor);

			// Register test DB context
			services.AddDbContext<ATSDBContext>(options =>
				options.UseNpgsql(_dbContainer.GetConnectionString()));

			services.RemoveAll<AuthApplicationDbContext>();
			services.RemoveAll<DbContextOptions<AuthApplicationDbContext>>();
			services.AddDbContext<AuthApplicationDbContext>(options =>
				options.UseNpgsql(
					_dbContainer.GetConnectionString(),
					npgsqlOptions => npgsqlOptions.MigrationsAssembly("APIs")));

			services.RemoveAll<IObjectStorageService>();
			services.AddSingleton<IObjectStorageService, MockObjectStorageService>();

			// Register HttpContextAccessor (scoped, not singleton)
			services.RemoveAll<IHttpContextAccessor>();
			services.AddScoped<IHttpContextAccessor>(_ =>
			{
				var fakeHttpContext = new DefaultHttpContext();
				fakeHttpContext.Response.Body = new MemoryStream();

				var claims = new List<Claim>
				{
					new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
				};

				fakeHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

				return new HttpContextAccessor { HttpContext = fakeHttpContext };
			});
		});
	}

	public async Task InitializeAsync()
	{
		await _dbContainer.StartAsync();

		using var scope = Services.CreateScope();
		var authDb = scope.ServiceProvider.GetRequiredService<AuthApplicationDbContext>();
		await authDb.Database.MigrateAsync();

		var db = scope.ServiceProvider.GetRequiredService<ATSDBContext>();
		await db.Database.MigrateAsync();
	}

	public async Task DisposeAsync()
	{
		await _dbContainer.StopAsync();
	}
}
