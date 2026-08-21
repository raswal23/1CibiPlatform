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
		services.Decorate<IATSRepository, ATSCacheRepository>();
		services.AddScoped<IApplicantSearchProjectionRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IApplicationFormRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IATSUserRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IBulkUploadRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IClientRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IDashboardRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IDisputeOrderRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IEmailInvitationRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IModuleRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IOrderHistoryRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IPackageRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IReportRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IRoleRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IUserClientRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IWithdrawnApplicationRepository>(provider => provider.GetRequiredService<IATSRepository>());
		services.AddScoped<IOrderHistoryFactory, OrderHistoryFactory>();
		services.AddScoped<IOrderHistoryService, OrderHistoryService>();
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
		services.AddScoped<IATSVerificationDataProvider, ATSVerificationDataProvider>();

		services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");
		services.AddScoped<IBulkSubmissionProcessorService, BulkSubmissionProcessorService>();
		services.AddScoped<IEmailNotificationProcessorService, EmailNotificationProcessorService>();
		services.AddScoped<IATSQueries, ATSQueries>();
		services.AddScoped<IAtsAccessClaimsProvider, AtsAccessClaimsProvider>();
		services.AddScoped<IAtsAssistantService, AtsAssistantService>();
		services.AddSingleton<AtsOrderDraftStore>();
		services.AddSingleton<AtsChatHistoryStore>();
		services.AddSignalR();

		services.ConfigureOptions<BulkSubmissionBackgroundJobSetup>();
		services.ConfigureOptions<EmailNotificationBackgroundJobSetup>();
		services.ConfigureOptions<ApplicantSearchProjectionJobSetup>();

		return services;
	}
	#endregion

	#region AI Assistant Config
	/// <summary>
	/// Registers the chat completion service and kernel used by the ATS assistant. ATS
	/// registers its own chat completion so automatic function calling is available for
	/// its <see cref="KernelFunctionAttribute"/> plugin.
	/// </summary>
	public static IServiceCollection AddATSAssistantConfiguration(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var endpoint = configuration.GetValue<string>("OpenAI:Endpoint");
		var apiKey = configuration.GetValue<string>("OpenAI:ApiKey");
		var model = configuration.GetValue<string>("OpenAI:Model");

		if (string.IsNullOrWhiteSpace(endpoint)
			|| string.IsNullOrWhiteSpace(apiKey)
			|| string.IsNullOrWhiteSpace(model))
		{
			return services;
		}

		services.AddOpenAIChatCompletion(
			modelId: model,
			endpoint: new Uri(endpoint),
			apiKey: apiKey);

		services.AddKernel();

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
