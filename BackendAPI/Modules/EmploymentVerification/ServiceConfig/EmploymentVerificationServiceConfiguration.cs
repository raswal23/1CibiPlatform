using ATS.Services.EmailService;

namespace EmploymentVerification.ServiceConfig;

public static class EmploymentVerificationServiceConfiguration
{
	#region Carter Config
	public static IServiceCollection AddEmploymentVerificationCarterModules(
		this IServiceCollection services,
		Assembly assembly)
	{
		services.AddCarter(new DependencyContextAssemblyCatalog([assembly]));

		return services;
	}
	#endregion

	#region MediatR Config
	public static IServiceCollection AddEmploymentVerificationMediaTR(
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
	#endregion

	#region Infrastructure Config
	public static IServiceCollection AddEmploymentVerificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddDbContext<EmploymentVerificationDbContext>(
			options => options.UseNpgsql(configuration.GetConnectionString("OnePlatform_Connection"), o => o.MigrationsAssembly("APIs")));
		return services;
	}
	#endregion

	#region Services Config

	public static IServiceCollection AddEmploymentVerificationServices(this IServiceCollection services)
	{
		// Scrutor decorates the concrete repository with the HybridCache behavior.
		services.AddScoped<
			IEmploymentVerificationRepository,
			EmploymentVerificationRepository>();
		services.Decorate<
			IEmploymentVerificationRepository,
			EmploymentVerificationCacheRepository>();
		services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");
		services.AddScoped<IEmploymentVerificationService, EmploymentVerificationService>();
		return services;
	}
	#endregion
}
