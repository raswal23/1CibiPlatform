using ATS.Data.Context;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Auth.Data.Context;
using Auth.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
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

			// Replace the keyed "ats" email sender — tests must not send real SMTP mail
			var atsEmailDescriptors = services
				.Where(s => s.ServiceType == typeof(IEmailService)
					&& s.IsKeyedService
					&& Equals(s.ServiceKey, "ats"))
				.ToList();

			foreach (var atsEmailDescriptor in atsEmailDescriptors)
			{
				services.Remove(atsEmailDescriptor);
			}

			services.AddKeyedSingleton<IEmailService, Test.BackendAPI.Infrastructure.Auth.Infrastructure.FakeEmailSender>("ats");

			// Register HttpContextAccessor (scoped, not singleton)
			services.RemoveAll<IHttpContextAccessor>();
			services.AddScoped<IHttpContextAccessor>(_ =>
			{
				var fakeHttpContext = new DefaultHttpContext();
				fakeHttpContext.Response.Body = new MemoryStream();

				var claims = new List<Claim>
				{
					new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
					new Claim(AuthClaimTypes.AtsRoleId, "1")
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
