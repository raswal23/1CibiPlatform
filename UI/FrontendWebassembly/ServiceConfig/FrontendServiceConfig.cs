namespace FrontendWebassembly.ServiceConfig;

public static class FrontendServiceConfig
{
	public static IServiceCollection AddFrontEndServices(this IServiceCollection services, IConfiguration configuration, Microsoft.AspNetCore.Components.WebAssembly.Hosting.IWebAssemblyHostEnvironment env)
	{
		// Allow configuration overrides
		var apiBaseFromConfig = configuration["ApiBase"];
		var ssoBaseFromConfig = configuration["SsoApiBase"];

		var isUat = string.Equals(env.Environment, "UAT", StringComparison.OrdinalIgnoreCase);

		var isSandbox = string.Equals(env.Environment, "Sandbox", StringComparison.OrdinalIgnoreCase);

		if (isSandbox)
		{
			apiBaseFromConfig ??= configuration["ApiBase"];
			ssoBaseFromConfig ??= configuration["SsoApiBase"];
		}

		if (isUat)
		{
			apiBaseFromConfig ??= configuration["ApiBase"];
			ssoBaseFromConfig ??= configuration["SsoApiBase"];
		}

		if (env.IsProduction())
		{
			apiBaseFromConfig ??= configuration["ApiBase"];
			ssoBaseFromConfig ??= configuration["SsoApiBase"];
		}

		if (!env.IsProduction() && !isUat && !isSandbox)
		{
			apiBaseFromConfig ??= configuration["ApiBase"];
			ssoBaseFromConfig ??= configuration["SsoApiBase"];
		}

		services.AddHttpClient("API", client =>
		{
			client.BaseAddress = new Uri(apiBaseFromConfig!);
		})
		 .AddHttpMessageHandler<CookieHandler>()
		 .AddHttpMessageHandler<InterceptorHandler>();

		// Client used by the interceptor to refresh token does NOT include the interceptor to avoid recursion
		services.AddHttpClient("RefreshAPI", client =>
		{
			client.BaseAddress = new Uri(apiBaseFromConfig!);
		})
		.AddHttpMessageHandler<CookieHandler>();


		services.AddHttpClient("SSOAPI", client =>
		{
			client.BaseAddress = new Uri(ssoBaseFromConfig!);
		})
		 .AddHttpMessageHandler<CookieHandler>();

		services.AddTransient<CookieHandler>();
		services.AddTransient<InterceptorHandler>();
		services.AddScoped<IRefreshTokenService, RefreshTokenService>();
		services.AddScoped<IAuthService, AuthService>();
		services.AddScoped<LocalStorageService>();
		services.AddScoped<EmailValidationService>();
		services.AddScoped<FileValidationService>();
		services.AddScoped<MobileNumberValidationService>();
		services.AddScoped<IAccessService, AccessService>();
		services.AddScoped<IPhilSysService, PhilSysService>();
		services.AddScoped<IUserManagementService, UserManagementService>();
		services.AddScoped<IUserProfileService, UserProfileService>();
		services.AddScoped<ISSOService, SSOService>();
		services.AddScoped<IAIAgentChatService, AIChatService>();
		services.AddScoped<IDialogWorkflowService, DialogWorkflowService>();
		services.AddScoped<IApplicationFormService, ApplicationFormService>();
		services.AddScoped<FrontendWebassembly.Services.EmploymentVerification.Interface.IEmploymentVerificationService, FrontendWebassembly.Services.EmploymentVerification.Implementation.EmploymentVerificationService>();
		services.AddScoped<IApplicationFormStateService, ApplicationFormStateService>();
		services.AddScoped<IEndorsementSubmissionService, EndorsementSubmissionService>();
		services.AddScoped<IDisputeOrderService, DisputeOrderService>();
		services.AddScoped<IReportService, ReportService>();
		services.AddScoped<IBulkUploadService, BulkUploadService>();
		services.AddScoped<CheckBulkFileName>();
		services.AddScoped<IOMSTicketingService, OMSTicketingService>();
		services.AddScoped<IDashboardService, DashboardService>();
		services.AddScoped<IPackageManagementService, PackageManagementService>();
		services.AddScoped<IClientManagementService, ClientManagementService>();
		services.AddScoped<IRoleManagementService, RoleManagementService>();
		services.AddScoped<IModuleManagementService, ModuleManagementService>();
		services.AddScoped<IATSUserManagementService, ATSUserManagementService>();
		services.AddScoped<IClientAssignmentService, ClientAssignmentService>();
		services.AddScoped<IAtsAssistantService, AtsAssistantService>();
		services.AddScoped<FrontendWebassembly.Services.Logging.IPlatformLogService, FrontendWebassembly.Services.Logging.PlatformLogService>();

		services.AddMudServices(config =>
		{
			config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
			config.SnackbarConfiguration.RequireInteraction = false;
			config.SnackbarConfiguration.PreventDuplicates = false;
			config.SnackbarConfiguration.NewestOnTop = false;
			config.SnackbarConfiguration.ShowCloseIcon = true;
			config.SnackbarConfiguration.VisibleStateDuration = 10000;
			config.SnackbarConfiguration.HideTransitionDuration = 500;
			config.SnackbarConfiguration.ShowTransitionDuration = 500;
			config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
		});

		return services;
	}
}
