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

		services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");
		services.AddScoped<IBulkSubmissionProcessorService, BulkSubmissionProcessorService>();
		services.AddScoped<IEmailNotificationProcessorService, EmailNotificationProcessorService>();
		services.AddScoped<IATSQueries, ATSQueries>();
		services.AddSignalR();

		services.ConfigureOptions<BulkSubmissionBackgroundJobSetup>();
		services.ConfigureOptions<EmailNotificationBackgroundJobSetup>();


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


		services.AddQuartz(q =>
		{
			q.SchedulerId = "ATS";

			q.SchedulerName = "ATS Scheduler";

			// This tells Quartz to create a pool of exactly 50 parallel threads
			//q.SetProperty("quartz.threadPool.threadCount", "50");

			q.UsePersistentStore(options =>
			{
				options.UsePostgres(postgres =>
				{
					postgres.ConnectionString =
						configuration.GetConnectionString(connStringSegment)
						?? throw new InvalidOperationException(
							$"Connection string '{connStringSegment}' was not found.");

					postgres.TablePrefix = "ats.qrtz_";
				});

				options.UseProperties = true;

				options.UseNewtonsoftJsonSerializer();

				options.UseClustering();
			});
		});

		services.AddQuartzHostedService(options =>
		{
			options.WaitForJobsToComplete = true;
		});

		return services;
    }
	#endregion

}
