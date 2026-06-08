namespace FrontendWebassembly.ServiceConfig;

public static class FrontendServiceConfig
{
	public static IServiceCollection AddFrontEndServices(this IServiceCollection services, IConfiguration configuration, Microsoft.AspNetCore.Components.WebAssembly.Hosting.IWebAssemblyHostEnvironment env)
	{
		// Allow configuration overrides
		var apiBaseFromConfig = configuration["ApiBase"];
		var ssoBaseFromConfig = configuration["SsoApiBase"];

		var isUat = string.Equals(env.Environment, "UAT", StringComparison.OrdinalIgnoreCase);

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

		if (!env.IsProduction() && !isUat)
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
		services.AddScoped<IAccessService, AccessService>();
		services.AddScoped<IPhilSysService, PhilSysService>();
		services.AddScoped<IUserManagementService, UserManagementService>();
		services.AddScoped<ICandidateService, CandidateService>();
		services.AddScoped<ISSOService, SSOService>();
		services.AddScoped<IAIAgentChatService, AIChatService>();
		services.AddScoped<IServerTableLoader, ServerTableLoader>();
		services.AddScoped<IDialogWorkflowService, DialogWorkflowService>();
		services.AddScoped<IApplicationFormService, ApplicationFormService>();
		services.AddScoped<IEndorsementSubmissionService, EndorsementSubmissionService>();

		return services;
	}
}
