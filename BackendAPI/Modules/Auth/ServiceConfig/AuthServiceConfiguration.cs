namespace Auth.ServiceConfig;

public static class AuthServiceConfiguration
{
	private const string assemblyName = "APIs";
	private const string connStringSegment = "OnePlatform_Connection";


	#region MediaTR Config
	public static IServiceCollection AddAuthMediaTR(this IServiceCollection services, Assembly assembly)
	{
		// Add MediatR
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
	public static IServiceCollection AddAuthServices(this IServiceCollection services)
	{
		services.AddHttpContextAccessor();
		services.AddScoped<ICurrentUser, CurrentUser>();
		services.AddTransient<AuthInitialData>();
		services.AddTransient<IPasswordHasherService, PasswordHasherService>();
		services.AddScoped<IJWTService, JWTService>();
		services.AddScoped<IAuthRepository, AuthRepository>();
		services.AddScoped<IRefreshTokenService, RefreshTokenService>();
		services.AddScoped<ILoginService, LoginService>();
		services.AddKeyedScoped<IEmailService, EmailService>("auth");
		services.AddScoped<IOtpService, OtpService>();
		services.AddScoped<IHashService, HashService>();
		services.AddScoped<IRegisterService, RegisterService>();
		services.AddScoped<IForgotPasswordService, ForgotPasswordService>();
		services.AddScoped<ISecureToken, SecureToken>();
		services.AddScoped<IApplicationService, ApplicationService>();
		services.AddScoped<ISubMenuService, SubMenuService>();
		services.AddScoped<IRoleService, RoleService>();
		services.AddScoped<IAppSubRoleService, AppSubRoleService>();
		services.AddScoped<IUserService, UserService>();
		services.AddScoped<ILockerUserService, LockedUserService>();
		services.AddScoped<IAuthQueries, AuthQueries>();

		services.Decorate<IAuthRepository, AuthCacheRepository>();
		services.AddScoped<IApplicationRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<IAppSubRoleRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<ILockoutRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<ILoginRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<IPasswordRecoveryRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<IRefreshTokenRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<IRegistrationRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<IRoleRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<ISubMenuRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<IUserDirectoryRepository>(provider => provider.GetRequiredService<IAuthRepository>());
		services.AddScoped<IUserRepository>(provider => provider.GetRequiredService<IAuthRepository>());

		return services;
	}

	#endregion

	#region Db Config

	public static IServiceCollection AddAuthInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddDbContext<AuthApplicationDbContext>(options =>
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
