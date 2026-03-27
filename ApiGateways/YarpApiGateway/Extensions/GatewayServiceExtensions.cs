using BuildingBlocks.SharedDTO;
using BuildingBlocks.SharedInterfaces;
using BackendAPI.Modules.Auth;
using BackendAPI.Modules.CNX;
using BackendAPI.Modules.PhilSys;
using BackendAPI.Modules.SSO;
using System.Reflection;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;
using BuildingBlocks.SharedConstants;
using AIAgent;

namespace ApiGateways.YarpApiGateway.Extensions;

public static class GatewayServiceExtensions
{
	// Registers CORS, Rate Limiting, module discovery, and YARP reverse proxy from modules
	#region Gateway Services
	public static void AddGatewayServices(this WebApplicationBuilder builder)
	{
		if (builder.Environment.IsDevelopment())
		{
			AddDevelopmentCors(builder.Services);
		}

		if (builder.Environment.IsProduction())
		{
			AddProductionCors(builder.Services);
		}
		AddRateLimiting(builder.Services);
		AddModuleDiscoveryAndReverseProxy(builder);
	}
	#endregion

	#region Private Methods

	private static void AddDevelopmentCors(IServiceCollection services)
	{
		services.AddCors(options =>
		{
			options.AddPolicy("CorsPolicy", policy =>
			{
				policy.WithOrigins("http://localhost:5134")
				.AllowCredentials()
				.AllowAnyMethod()
				.AllowAnyHeader();
			});
		});
	}

	private static void AddProductionCors(IServiceCollection services)
	{
		services.AddCors(options =>
		{
			options.AddPolicy("CorsPolicy", policy =>
			{
				policy.WithOrigins(
					"https://apps.cibi.com.ph/oms",
					"https://apps.cibi.com.ph/oms_uat")
				.AllowCredentials()
				.AllowAnyMethod()
				.AllowAnyHeader();
			});
		});
	}
	#endregion

	#region Rate Limiting

	private static void AddRateLimiting(IServiceCollection services)
	{
		services.AddRateLimiter(options =>
		{
			options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
			{
				string policyName = GatewayConstants.RateLimitPolicies.Default;
				var endpoint = httpContext.GetEndpoint();
				if (endpoint is not null)
				{
					var md = endpoint.Metadata.GetMetadata<IReadOnlyDictionary<string, string>>();
					if (md is not null && md.TryGetValue("RateLimitPolicy", out var configured))
					{
						policyName = configured ?? "default";
					}
				}

				return policyName switch
				{
					GatewayConstants.RateLimitPolicies.LoginPolicy => RateLimitPartition.GetFixedWindowLimiter(policyName, _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 5,
						Window = TimeSpan.FromSeconds(10),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0
					}),

					GatewayConstants.RateLimitPolicies.DefaultStrict => RateLimitPartition.GetFixedWindowLimiter(policyName, _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 20,
						Window = TimeSpan.FromMinutes(1),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0
					}),

					_ => RateLimitPartition.GetFixedWindowLimiter(GatewayConstants.RateLimitPolicies.Default, _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 500,
						Window = TimeSpan.FromSeconds(1),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0
					})
				};
			});

			options.RejectionStatusCode = 429;
		});
	}
	#endregion

	#region Module Discovery

	private static void AddModuleDiscoveryAndReverseProxy(WebApplicationBuilder builder)
	{
		// Explicit assembly scanning using module markers to avoid scanning problematic assemblies
		var assembliesToScan = new[]
		{
			Assembly.GetExecutingAssembly(),
			typeof(AuthMarker).Assembly,
			typeof(CNXMarker).Assembly,
			typeof(PhilSysMarker).Assembly,
			typeof(SSOMarker).Assembly,
			typeof(AIAgentMarker).Assembly
		};

		builder.Services.Scan(scan =>
		{
			scan.FromAssemblies(assembliesToScan)
			.AddClasses(classes => classes.AssignableTo<IReverseProxyModule>())
			.AsImplementedInterfaces()
			.WithSingletonLifetime();
		});

		// Build a temporary provider to resolve discovered modules and collect DTOs
		using var tempProvider = builder.Services.BuildServiceProvider();
		var modules = tempProvider.GetServices<IReverseProxyModule>().ToList();

		Console.WriteLine($"[Gateway] Discovered {modules.Count} IReverseProxyModule instances via explicit assembly scan.");

		var routeDtos = new List<RouteDefinitionDTO>();
		var clusterDtos = new List<ClusterDefinitionDTO>();

		foreach (var module in modules)
		{
			routeDtos.AddRange(module.GetRoutes());
			clusterDtos.AddRange(module.GetClusters());
		}

		Console.WriteLine($"[Gateway] Collected {routeDtos.Count} routes and {clusterDtos.Count} clusters from modules.");

		// Convert DTOs into YARP config objects
		var yarpRoutes = routeDtos.Select(r =>
		{
			var rc = new RouteConfig
			{
				RouteId = r.RouteId,
				ClusterId = r.ClusterId,
				Match = new RouteMatch { Path = r.MatchPath, Methods = r.Methods?.ToArray() },
				Transforms = r.Transforms?.Select(kv => new Dictionary<string, string> { { kv.Key, kv.Value } }).ToArray()
			};

			if (r.Metadata is not null)
			{
				return new RouteConfig
				{
					RouteId = rc.RouteId,
					ClusterId = rc.ClusterId,
					Match = rc.Match,
					Transforms = rc.Transforms,
					Metadata = new Dictionary<string, string>(r.Metadata)
				};
			}

			return rc;
		}).ToList();

		var yarpClusters = clusterDtos.Select(c => new ClusterConfig
		{
			ClusterId = c.ClusterId,
			Destinations = c.Destinations.ToDictionary(d => d.Id, d => new DestinationConfig { Address = d.Address })
		}).ToList();

		// register route catalog for diagnostics
		var catalog = new ApiGateways.YarpApiGateway.Services.RouteCatalog
		{
			Routes = yarpRoutes.Select(r => new RouteDefinitionDTO(r.RouteId, r.Match?.Path ?? string.Empty, r.ClusterId, r.Match?.Methods, r.Transforms?.ToDictionary(t => t.Keys.First(), t => t.Values.First()), r.Metadata?.ToDictionary(k => k.Key, k => k.Value))).ToList(),
			Clusters = yarpClusters.Select(c => new ClusterDefinitionDTO(c.ClusterId, c.Destinations.Select(d => new DestinationDefinitionDTO(d.Key, d.Value.Address)))).ToList()
		};
		builder.Services.AddSingleton(catalog as ApiGateways.YarpApiGateway.Services.RouteCatalog);

		builder.Services.AddReverseProxy()
		.LoadFromMemory(yarpRoutes, yarpClusters);
	}
	#endregion
}
