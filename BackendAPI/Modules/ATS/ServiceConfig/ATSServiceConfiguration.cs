namespace ATS.ServiceConfig;
public static class ATSServiceConfiguration
{
    private const string assemblyName = "APIs";
    private const string connStringSegment = "OnePlatform_Connection";

    #region Carter Config
    //public static IServiceCollection AddATSCarterModules(this IServiceCollection services, Assembly assembly)
    //{
    //    services.AddCarter(configurator: c =>
    //    {
    //        var modules = assembly.GetTypes()
				//.Where(t => typeof(ICarterModule).IsAssignableFrom(t) && !t.IsAbstract)
    //            .ToArray();
    //        c.WithModules(modules);
    //    });
    //    return services;
    //}
    #endregion

    #region MediatR Config
    public static IServiceCollection AddATSMediaTR(this IServiceCollection services, Assembly assembly)
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
    public static IServiceCollection AddATSServices(this IServiceCollection services)
    {
		services.AddScoped<IApplicationFormService, ApplicationFormService>();
		services.AddScoped<IATSRepository, ATSRepository>();
		services.AddScoped<IUnitOfWork, UnitOfWork>();
		services.AddScoped<IEndorsementSubmissionService, EndorsementSubmissionService>();

		services.AddHostedService<BulkSubmissionBackgroundService>();
		services.AddHostedService<EmailNotificationBackgroundServiceForPending>();
		services.AddHostedService<EmailNotificationBackgroundServiceForError>();
		services.AddScoped<IATSQueries, ATSQueries>();
		services.AddSignalR();

		return services;
    }
    #endregion

    #region Db Config
    public static IServiceCollection AddATSInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ATSDBContext>(options =>
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
