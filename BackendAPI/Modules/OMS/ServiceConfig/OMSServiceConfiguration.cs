namespace OMS.ServiceConfig;

public static class OMSServiceConfiguration
{
	#region Carter Config
	public static IServiceCollection AddOMSCarterModules(
		this IServiceCollection services,
		Assembly assembly)
	{
		services.AddCarter(new DependencyContextAssemblyCatalog([assembly]));

		return services;
	}
	#endregion

	#region MediatR Config
	public static IServiceCollection AddOMSMediaTR(
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
	public static IServiceCollection AddOMSInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// The OMS module has no DbContext: all persistence goes through the
		// legacy SQL Server stored procedures over this connection factory.
		services.AddSingleton<IOMSSqlConnectionFactory>(
			new OMSSqlConnectionFactory(
				configuration.GetConnectionString("OMS_Connection") ?? string.Empty));

		return services;
	}
	#endregion

	#region Services Config
	public static IServiceCollection AddOMSServices(this IServiceCollection services)
	{
		// No cache decorator by design: ticket creation is a write path and the
		// requestor/PO validations must always hit the live OMS database.
		services.AddScoped<IOMSRepository, OMSRepository>();
		services.AddScoped<IOMSTicketCreator, OMSTicketCreator>();

		return services;
	}
	#endregion
}
