using Auth.Data.Context;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Quartz;
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

		var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
		DotEnvLoader.Load(Path.Combine(solutionRoot, ".env"));

		builder.ConfigureTestServices(services =>
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

			// The real provider queries the ATS database, which is not available here
			services.RemoveAll<global::Auth.Shared.Contracts.IAtsAccessClaimsProvider>();
			services.AddScoped<global::Auth.Shared.Contracts.IAtsAccessClaimsProvider, FakeAtsAccessClaimsProvider>();
		});
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
