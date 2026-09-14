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

			// Last, so validation runs first: a request rejected as invalid never reached
			// a handler and must not be recorded as an action someone took.
			config.AddOpenBehavior(typeof(AtsAuditBehavior<,>));
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

		// No cache decorator: bulk upload status changes every Quartz tick, so a cached
		// first page would defeat the dashboard this repository feeds.
		services.AddScoped<IBulkUploadDashboardRepository, BulkUploadRepository>();

		// Same reasoning: TicketStatus moves within one Quartz tick, and the claim
		// query must never be served from a cache.
		services.AddScoped<IOMSTicketingRepository, OMSTicketingRepository>();


		// Also uncached: the audit trail is append-only and the screen exists to show what
		// just happened, so a cached first page would hide the newest action.
		services.AddScoped<IAtsAuditRepository, AtsAuditRepository>();
		services.AddScoped<IAtsAuditService, AtsAuditService>();

		// Singleton: the queue has to outlive the request scope that writes to it. The
		// concrete type is registered as well so the drain can read the channel - see the
		// note on AtsAuditDrainService's constructor.
		services.AddSingleton<AtsAuditWriter>();
		services.AddSingleton<IAtsAuditWriter>(provider => provider.GetRequiredService<AtsAuditWriter>());
		services.AddHostedService<AtsAuditDrainService>();
		services.AddHostedService<AtsAuditRetentionService>();

		// Uncached for the same reason as the two above: the bell exists to show what just
		// happened, so a cached unread count would hide the notification raised a second ago.
		services.AddScoped<IAtsNotificationRepository, AtsNotificationRepository>();
		services.AddScoped<IAtsNotificationService, AtsNotificationService>();
		services.AddHostedService<AtsNotificationRetentionService>();

		// An integrating client polls these to watch an order move, so a cached read
		// would report exactly the staleness they are polling to avoid.
		services.AddScoped<IPublicApiRepository, PublicApiRepository>();
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
		services.AddScoped<IAtsEmailAccountManagementService, AtsEmailAccountManagementService>();
		services.AddScoped<IClientManagementService, ClientManagementService>();
		services.AddScoped<IRoleManagementService, RoleManagementService>();
		services.AddScoped<IModuleManagementService, ModuleManagementService>();
		services.AddScoped<IUserManagementService, UserManagementService>();
		services.AddScoped<IClientAssignmentService, ClientAssignmentService>();
		services.AddScoped<IATSVerificationDataProvider, ATSVerificationDataProvider>();
		services.AddScoped<IAtsAccessScopeResolver, AtsAccessScopeResolver>();

		// Shared by the web console, the public API and the bulk parser so all three
		// agree on what a valid package and order type are.
		services.AddScoped<IOrderInputValidator, OrderInputValidator>();
		services.AddScoped<IBulkUploadMonitoringService, BulkUploadMonitoringService>();

		// The registry owns one pool and one limiter PER REGISTERED ACCOUNT. Neither is
		// registered directly any more: there is no longer exactly one of each, so asking the
		// container for "the pool" has no answer.
		//
		// The reason they were singletons still holds and is now enforced inside the registry -
		// both bound a resource belonging to the SENDING ACCOUNT rather than to a request. A
		// per-scope pool is not a pool; it would reopen and re-authenticate a connection per
		// operation, which is the exact behaviour that got this sender throttled at 14
		// messages. A per-scope limiter would let two concurrent passes each run at the full
		// rate and double the real one. What changed is the scope of "the sender": from the
		// process to one mailbox.
		services.AddSingleton<ISmtpAccountPoolRegistry, SmtpAccountPoolRegistry>();

		// Deliberately uncached. Health and consumption change on every send, and the selector
		// reads them per message - a cache here would route mail to an account that is already
		// cooling down, which is the failure this whole feature exists to avoid.
		services.AddScoped<IAtsEmailAccountRepository, AtsEmailAccountRepository>();

		// Drops send-log rows past the retention window. The rolling-24h count only ever reads
		// the last day; without this the table grows forever to serve a query that never looks
		// at the old rows.
		services.AddHostedService<AtsEmailSendLogRetentionService>();

		// Singleton: stateless past its constructor, which reads and validates the key once
		// so a missing or malformed one fails at start-up rather than at the first send.
		services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();

		services.AddKeyedScoped<IEmailService, ATSEmailService>("ats");

		// The management service sends the registration/edit OTP through the CANDIDATE
		// account's own credentials (see AtsEmailAccountManagementService's remark), not
		// through whichever account the keyed IEmailService above happens to resolve to -
		// so it depends on the plain, unkeyed interface. ATSEmailService implements both;
		// this just gives the container an answer for the unkeyed one too.
		services.AddScoped<IAtsEmailSender>(provider =>
			(IAtsEmailSender)provider.GetRequiredKeyedService<IEmailService>("ats"));
		services.AddScoped<IBulkSubmissionProcessorService, BulkSubmissionProcessorService>();
		services.AddScoped<IEmailNotificationProcessorService, EmailNotificationProcessorService>();
		services.AddScoped<IOMSTicketingProcessorService, OMSTicketingProcessorService>();
		services.AddScoped<IOMSTicketingMonitoringService, OMSTicketingMonitoringService>();
		services.AddScoped<IPublicApiService, PublicApiService>();
		services.AddScoped<IATSQueries, ATSQueries>();
		services.AddScoped<IAtsAccessClaimsProvider, AtsAccessClaimsProvider>();
		services.AddScoped<IAtsAssistantService, AtsAssistantService>();
		services.AddSingleton<AtsOrderDraftStore>();
		services.AddSingleton<AtsChatHistoryStore>();
		services.AddSignalR();

		services.ConfigureOptions<BulkSubmissionBackgroundJobSetup>();
		services.ConfigureOptions<EmailNotificationBackgroundJobSetup>();
		services.ConfigureOptions<ApplicantSearchProjectionJobSetup>();
		services.ConfigureOptions<OMSTicketingBackgroundJobSetup>();

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
		// Every AtsAuditOptions value has a working default, so an absent section is
		// valid: the audit trail runs with the agreed 30-day retention out of the box.
		services.Configure<AtsAuditOptions>(
			configuration.GetSection(AtsAuditOptions.SectionName));

		// Same story: absent section means the agreed 30-day notification retention.
		services.Configure<AtsNotificationOptions>(
			configuration.GetSection(AtsNotificationOptions.SectionName));

		// And again for SMTP throughput. The safe send rate belongs to the provider, not to
		// the code, so finding it for a new one must not require a redeploy.
		services.Configure<AtsEmailDeliveryOptions>(
			configuration.GetSection(AtsEmailDeliveryOptions.SectionName));

		// The audit change collector and its interceptor are scoped, so the context is
		// built from the request's provider rather than a static lambda.
		services.AddScoped<IAtsAuditChangeCollector, AtsAuditChangeCollector>();
		services.AddScoped<AtsAuditChangeInterceptor>();

		services.AddDbContext<ATSDBContext>((serviceProvider, options) =>
		{
			options.UseNpgsql(
				configuration.GetConnectionString(connStringSegment),
				npgsqlOptions => npgsqlOptions.MigrationsAssembly(assemblyName)
			);

			// Captures before/after values for the audit trail. Reads the change tracker
			// only - it never writes, and a command that saves nothing costs nothing.
			options.AddInterceptors(serviceProvider.GetRequiredService<AtsAuditChangeInterceptor>());
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
