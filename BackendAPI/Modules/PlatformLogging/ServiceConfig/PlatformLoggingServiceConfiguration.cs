using PlatformLogging.BackgroundJobs;
using PlatformLogging.Configuration;
using PlatformLogging.Infrastructure;

namespace PlatformLogging.ServiceConfig;

public static class PlatformLoggingServiceConfiguration
{
	public static IServiceCollection AddPlatformLoggingMediaTR(
		this IServiceCollection services,
		Assembly assembly)
	{
		services.AddMediatR(config =>
		{
			config.RegisterServicesFromAssembly(assembly);
			config.AddOpenBehavior(typeof(ValidationBehavior<,>));
			config.AddOpenBehavior(typeof(LoggingBehavior<,>));
		});

		services.AddValidatorsFromAssembly(assembly);
		services.AddExceptionHandler<CustomExceptionHandler>();

		return services;
	}

	public static IServiceCollection AddPlatformLoggingServices(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.Configure<PlatformLoggingOptions>(
			configuration.GetSection(PlatformLoggingOptions.SectionName));

		services.AddScoped<IPlatformLogRepository, PlatformLogRepository>();
		services.AddScoped<IPlatformLogService, PlatformLogService>();
		services.AddHostedService<PlatformLogRetentionService>();

		return services;
	}

	public static IServiceCollection AddPlatformLoggingInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddDbContext<PlatformLoggingDBContext>(options =>
		{
			options.UseNpgsql(
				configuration.GetConnectionString("OnePlatform_Connection"),
				npgsqlOptions => npgsqlOptions.MigrationsAssembly("APIs"));
		});

		return services;
	}

	public static PostgreSqlBatchingSink? CreatePlatformLogSink(
		this IConfiguration configuration)
	{
		var options = configuration
			.GetSection(PlatformLoggingOptions.SectionName)
			.Get<PlatformLoggingOptions>() ?? new PlatformLoggingOptions();

		var connectionString = configuration.GetConnectionString(
			options.ConnectionStringName);

		if (!options.PostgreSqlEnabled || string.IsNullOrWhiteSpace(connectionString))
		{
			return null;
		}

		return new PostgreSqlBatchingSink(connectionString, options);
	}
}
