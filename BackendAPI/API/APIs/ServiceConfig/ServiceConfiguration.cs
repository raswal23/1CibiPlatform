namespace APIs.ServiceConfig;

public static class ServiceConfiguration
{

	private static readonly Assembly _authAssembly = typeof(AuthMarker).Assembly;
	private static readonly Assembly _cnxAssembly = typeof(CNXMarker).Assembly;
	private static readonly Assembly _philsysAssembly = typeof(PhilSysMarker).Assembly;
	private static readonly Assembly _ssoAssembly = typeof(SSOMarker).Assembly;
	private static readonly Assembly _aiAgentAssembly = typeof(AIAgentMarker).Assembly;
	private static readonly Assembly _atsAssembly = typeof(ATSMarker).Assembly;
	private static readonly Assembly _platformLoggingAssembly = typeof(PlatformLoggingMarker).Assembly;
	private static readonly Assembly _employmentVerificationAssembly = typeof(EmploymentVerificationMarker).Assembly;


	#region Logging Config

	public static IServiceCollection AddLoggingConfiguration(
		this IServiceCollection services,
		IConfiguration configuration,
		IHostEnvironment environment)
	{
		var loggerConfiguration = new LoggerConfiguration()
				   .MinimumLevel.Information()
				   .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
				   .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
				   .Enrich.FromLogContext()
				   .Enrich.WithProperty("Platform", "1CibiPlatform")
				   .Enrich.WithProperty("Environment", environment.EnvironmentName)
				   .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());

		var sink = configuration.CreatePlatformLogSink();
		if (sink is not null)
		{
			services.AddSingleton(sink);
			services.AddSingleton<IHostedService>(sink);
			loggerConfiguration.WriteTo.Sink(sink, LogEventLevel.Warning);
		}

		Log.Logger = loggerConfiguration.CreateLogger();

		services.AddLogging(builder =>
		{
			builder.ClearProviders();
			builder.AddSerilog(dispose: true);
			builder.AddDebug();
		});

		return services;
	}

	#endregion

	#region Observability Config

	/// <summary>
	/// Registers the health checks that back both the <c>/health</c> endpoint and the
	/// Prometheus <c>aspnetcore_healthcheck_status</c> gauge scraped by the monitoring
	/// server. Every check registered here becomes its own time series, so Grafana can
	/// show per-dependency health rather than a single opaque up/down.
	/// </summary>
	public static IServiceCollection AddObservability(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var databaseConnection = configuration.GetConnectionString("OnePlatform_Connection");
		var redisConnection = configuration.GetConnectionString("TairRedis");

		var healthChecks = services.AddHealthChecks()
			.ForwardToPrometheus();

		// Postgres (RDS) is the one hard dependency: without it the API can serve nothing
		// useful, so it is tagged "ready" and will flip /health/ready to Unhealthy.
		if (!string.IsNullOrWhiteSpace(databaseConnection))
		{
			healthChecks.AddNpgSql(
				databaseConnection,
				name: "postgres",
				tags: ["ready", "db"]);
		}

		// Redis is a cache, not a source of truth - HybridCache falls back to its
		// in-memory L1 when the distributed L2 is unreachable. It is therefore reported
		// as Degraded and left out of the "ready" tag, so a Redis blip does not pull
		// instances out of rotation. It still surfaces as its own Grafana time series.
		if (!string.IsNullOrWhiteSpace(redisConnection))
		{
			healthChecks.AddRedis(
				redisConnection,
				name: "redis",
				failureStatus: HealthStatus.Degraded,
				tags: ["cache"]);
		}

		return services;
	}

	#endregion

	#region Environment Config

	public static void ConfigureEnvironment(
		this IServiceCollection services,
		WebApplicationBuilder builder)
	{
		if (builder.Environment.IsDevelopment())
		{
			builder.Services.ConfigureCorsDev();
		}

		if (builder.Environment.IsProduction())
		{
			builder.Services.ConfigureCorsProd();
		}
	}


	#endregion

	#region CORS
	public static void ConfigureCorsProd(this IServiceCollection services) =>
		services.AddCors(options =>
		{
			options.AddPolicy("CorsPolicy",
					 policy =>
					 {
						 policy.WithOrigins(
							 "http://192.168.34.20:4200",
							 "http://localhost:5055",
							 "https://apps.cibi.com.ph/oms",
							 "https://apps.cibi.com.ph/oms_uat")
							 .AllowAnyHeader()
							 .AllowAnyMethod()
							 .AllowCredentials();
					 });
		});

	public static void ConfigureCorsDev(this IServiceCollection services) =>
		services.AddCors(options =>
		{
			options.AddPolicy("CorsPolicy",
					 policy =>
					 {
						 policy.WithOrigins(
							 "http://localhost:5123",
							 "http://localhost:5134")
							 .AllowAnyHeader()
							 .AllowAnyMethod()
							 .AllowCredentials();
					 });
		});
	#endregion

	#region JWT Config and SSO Config
	public static IServiceCollection AddJwtAuthentication(
		this IServiceCollection services,
		IConfiguration configuration,
		IHostEnvironment environment)
	{
		// JWT Authentication
		var jwtSettings = configuration.GetSection("Jwt");
		var key = jwtSettings["Key"];
		var issuer = jwtSettings["Issuer"];
		var audience = jwtSettings["Audience"];
		var expiryInMinutes = int.Parse(jwtSettings["ExpiryInMinutes"]!);
		var _httpCookieOnlyKey = configuration.GetValue<string>("HttpCookieOnlyKey");
		var _signinScheme = configuration.GetValue<string>("SSOMetadata:SigninScheme");
		// SAML2 Configuration
		var spBaseUrl = configuration["Saml2:SpBaseUrl"];
		var idpMetadataUrl = configuration["Saml2:IdpMetadataUrl"];
		var idpEntityId = configuration["Saml2:IdpEntityId"];

		services.AddAuthentication(x =>
		{
			x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
			x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			x.DefaultSignInScheme = _signinScheme;
			x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
		})
		.AddJwtBearer(options =>
		{
			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,
				ValidIssuer = issuer,
				ValidAudience = audience,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)),
				RoleClaimType = ClaimTypes.Role,
				ClockSkew = TimeSpan.Zero
			};
			options.Events = new JwtBearerEvents
			{
				OnMessageReceived = context =>
				{
					if (context.Request.Cookies.TryGetValue(_httpCookieOnlyKey!, out var token))
					{
						context.Token = token;
					}
					return Task.CompletedTask;
				},
				OnTokenValidated = async context =>
				{
					var sessionClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
					if (string.IsNullOrWhiteSpace(sessionClaim))
						return; // API/SSO tokens without a browser refresh session keep their existing behavior.

					var userClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
					if (!int.TryParse(sessionClaim, out var sessionId) || !Guid.TryParse(userClaim, out var userId))
					{
						context.Fail("Invalid authentication session.");
						return;
					}

					var validator = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionValidator>();
					if (!await validator.IsActiveAsync(sessionId, userId, context.HttpContext.RequestAborted))
						context.Fail("Authentication session is no longer active.");
				}
			};
		})
		.AddCookie(_signinScheme!, options =>
		{
			options.Cookie.Name = _signinScheme;
			options.Cookie.HttpOnly = true;
			options.Cookie.Path = "/";
			options.ExpireTimeSpan = TimeSpan.FromHours(8);
			options.SlidingExpiration = true;

			// Environment-specific settings
			if (environment.IsDevelopment())
			{
				// Development/Ngrok settings
				options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
				options.Cookie.SameSite = SameSiteMode.None;
			}
			else
			{
				options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
				options.Cookie.SameSite = SameSiteMode.Lax;
				// Optionally set domain for subdomain sharing
				// options.Cookie.Domain = ".yourdomain.com";
			}
		})
		.AddSaml2(options =>
		{
			options.SPOptions.EntityId = new EntityId(spBaseUrl);
			options.SPOptions.ReturnUrl = new Uri(spBaseUrl + "/sso/login/callback");
			var identityProvider = new IdentityProvider(
				new EntityId(idpEntityId),
				options.SPOptions)
			{
				MetadataLocation = idpMetadataUrl,
				LoadMetadata = true,
				AllowUnsolicitedAuthnResponse = true
			};
			options.IdentityProviders.Add(identityProvider);
		});

		services.AddOptions<Saml2Options>().ValidateOnStart();
		services.AddAuthorization();
		return services;
	}
	#endregion

	#region Db Config

	public static IServiceCollection AddModuleInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Add DbContext
		services.AddAuthInfrastructure(configuration);
		services.AddPhilSysInfrastructure(configuration);
		services.AddAIAgentInfrastructure(configuration);
		services.AddATSInfrastructure(configuration);
		services.AddEmploymentVerificationInfrastructure(configuration);
		services.AddPlatformLoggingInfrastructure(configuration);
		return services;
	}
	#endregion

	#region Carter Config
	public static IServiceCollection AddModuleCarter(this IServiceCollection services)
	{
		services.AddCarter(new DependencyContextAssemblyCatalog([
			 _authAssembly,
			 _cnxAssembly,
			 _philsysAssembly,
			 _ssoAssembly,
			 _aiAgentAssembly,
			 _atsAssembly,
			 _platformLoggingAssembly
			 ,_employmentVerificationAssembly
		 ]));


		return services;
	}
	#endregion

	#region MediaTR Config

	public static IServiceCollection AddModuleMediaTR(
		this IServiceCollection services)
	{
		// Add MediaTR
		services.AddAuthMediaTR(_authAssembly);
		services.AddCNXMediaTR(_cnxAssembly);
		services.AddPhilSysMediaTR(_philsysAssembly);
		services.AddSSOMediaTR(_ssoAssembly);
		services.AddAIAgentMediaTR(_aiAgentAssembly);
		services.AddATSMediaTR(_atsAssembly);
		services.AddPlatformLoggingMediaTR(_platformLoggingAssembly);
		services.AddEmploymentVerificationMediaTR(_employmentVerificationAssembly);
		return services;
	}

	#endregion

	#region Services Config
	public static IServiceCollection AddModuleServices(this IServiceCollection services, IConfiguration configuration)
	{
		// Add Services
		services.AddAuthServices();
		services.AddCNXServices();
		services.AddPhilSysServices();
		services.AddSSOServices();
		services.AddAIAgentServices();
			services.AddATSServices();
			services.AddATSAssistantConfiguration(configuration);
			services.AddEmploymentVerificationServices();
		services.AddPlatformLoggingServices(configuration);
		return services;
	}
	#endregion

	#region Hybrid Cache Config

	public static IServiceCollection AddHybridCaches(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var redisConnection = configuration.GetConnectionString("TairRedis");

		services.AddStackExchangeRedisCache(options =>
		{
			options.Configuration = redisConnection;
			options.InstanceName = "oneplatform:";
		});

		services.AddHybridCache(options =>
		{
			options.DefaultEntryOptions = new HybridCacheEntryOptions
			{
				Expiration = TimeSpan.FromMinutes(10),
				LocalCacheExpiration = TimeSpan.FromMinutes(10),
				Flags = HybridCacheEntryFlags.DisableDistributedCache
			};
		});

		return services;
	}
	#endregion

	#region Alibaba Oss Config

	public static IServiceCollection AddAlibabaOssConfiguration(
		this IServiceCollection services,
		IConfiguration configuration)
	{

		services.AddAlibabaStorage(configuration);

		return services;
	}

	#endregion

	#region AI Agent Config
	public static IServiceCollection AddAIAgentConfigurationSkills(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddAIAgentSkills(configuration);
		return services;
	}
	#endregion

}
