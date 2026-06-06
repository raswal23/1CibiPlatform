namespace PhilSys.ServiceConfig;
public static class PhilSysServiceConfiguration
{
	private const string assemblyName = "APIs";
	private const string connStringSegment = "OnePlatform_Connection";

	#region Carter Config
	public static IServiceCollection AddPhilSysCarterModules(this IServiceCollection services, Assembly assembly)
	{
		services.AddCarter(configurator: c =>
		{
			var modules = assembly.GetTypes()
				.Where(t => typeof(ICarterModule).IsAssignableFrom(t) && !t.IsAbstract)
				.ToArray();
			c.WithModules(modules);
		});
		return services;
	}
	#endregion

	#region MediatR Config
	public static IServiceCollection AddPhilSysMediaTR(this IServiceCollection services, Assembly assembly)
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

	#region Services
	public static IServiceCollection AddPhilSysServices(this IServiceCollection services)
	{
		services.AddHttpClient("PhilSys", client =>
		{
			client.BaseAddress = new Uri("https://ws.everify.gov.ph/api/");

			client.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));
		});
		services.AddScoped<PartnerSystemService>();
		services.AddScoped<UpdateFaceLivenessSessionService>();
		services.AddScoped<LivenessSessionService>();
		services.AddScoped<DeleteTransactionService>();
		services.AddScoped<GetLivenessKeyService>();
		services.AddScoped<IPhilSysService, PhilSysService>();
		services.AddScoped<IHashService, HashService>();
		services.AddScoped<ISecureToken, SecureToken>();
		services.AddScoped<IPhilSysRepository, PhilSysRepository>();
		services.AddScoped<IUnitOfWork, UnitOfWork>();
		return services;
	}
	#endregion

	#region Db Config
	public static IServiceCollection AddPhilSysInfrastructure(
		this IServiceCollection services, 
		IConfiguration configuration)
	{
		services.AddDbContext<PhilSysDBContext>(options =>
		{
			options.UseNpgsql(
				configuration.GetConnectionString(connStringSegment),
				npgsqlOptions => npgsqlOptions.MigrationsAssembly(assemblyName)
				);

		});
		return services;
	}
	#endregion
}