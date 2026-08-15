namespace ATS.ServiceConfig;
public static class ATSServiceConfiguration
{
    private const string assemblyName = "APIs";
    private const string connStringSegment = "OnePlatform_Connection";

	#region Carter Config
	public static IServiceCollection AddATSCarterModules(this IServiceCollection services, Assembly assembly)
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
		services.AddTransient<ATSInitialData>();
		services.AddScoped<IApplicationFormService, ApplicationFormService>();
		services.AddScoped<IATSRepository, ATSRepository>();
		services.AddScoped<IOrderHistoryRepository, OrderHistoryRepository>();
		services.AddScoped<IOrderHistoryFactory, OrderHistoryFactory>();
		services.AddScoped<IOrderHistoryService, OrderHistoryService>();
		services.Decorate<IATSRepository, ATSCacheRepository>();
		services.AddScoped<IPackageRepository, PackageRepository>();
		services.Decorate<IPackageRepository, PackageCacheRepository>();
		services.AddScoped<IClientRepository, ClientRepository>();
		services.Decorate<IClientRepository, ClientCacheRepository>();
		services.AddScoped<IRoleRepository, RoleRepository>();
		services.Decorate<IRoleRepository, RoleCacheRepository>();
		services.AddScoped<IModuleRepository, ModuleRepository>();
		services.Decorate<IModuleRepository, ModuleCacheRepository>();
		services.AddScoped<IATSUserRepository, ATSUserRepository>();
		services.Decorate<IATSUserRepository, ATSUserCacheRepository>();
		services.AddScoped<IUserClientRepository, UserClientRepository>();
		services.Decorate<IUserClientRepository, UserClientCacheRepository>();
		services.AddScoped<IUnitOfWork, UnitOfWork>();
		services.AddScoped<IEndorsementSubmissionService, EndorsementSubmissionService>();
		services.AddScoped<IDisputeOrderService, DisputeOrderService>();
		services.AddScoped<IReportService, ReportService>();
		services.AddScoped<IDashboardService, DashboardService>();
		services.AddScoped<IApplicantSearchProjectionService, ApplicantSearchProjectionService>();
		services.AddScoped<IFilePdfService, FilePdfService>();
		services.AddScoped<IPackageManagementService, PackageManagementService>();
		services.AddScoped<IClientManagementService, ClientManagementService>();
		services.AddScoped<IRoleManagementService, RoleManagementService>();
		services.AddScoped<IModuleManagementService, ModuleManagementService>();
		services.AddScoped<IUserManagementService, UserManagementService>();
		services.AddScoped<IClientAssignmentService, ClientAssignmentService>();

		services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");
		services.AddScoped<IBulkSubmissionProcessorService, BulkSubmissionProcessorService>();
		services.AddScoped<IEmailNotificationProcessorService, EmailNotificationProcessorService>();
		services.AddScoped<IEmailNotificationRecoveryService, EmailNotificationRecoveryService>();
		services.AddScoped<IATSQueries, ATSQueries>();
		services.AddScoped<IAtsAccessClaimsProvider, AtsAccessClaimsProvider>();
		services.AddSignalR();

		services.ConfigureOptions<BulkSubmissionBackgroundJobSetup>();
		services.ConfigureOptions<EmailNotificationBackgroundJobSetup>();
		services.ConfigureOptions<ApplicantSearchProjectionJobSetup>();
		services.ConfigureOptions<EmailNotificationRecoveryJobSetup>();

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
