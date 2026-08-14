namespace PlatformLogging.Path;

public sealed class PlatformLoggingPaths : IReverseProxyModule
{
	public IEnumerable<RouteDefinitionDTO> GetRoutes()
	{
		return new[]
		{
			new RouteDefinitionDTO(
				RouteId: "GetPlatformLogsEntryPoint",
				MatchPath: "/platform-logging/logs",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new[] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/platform-logging/logs" }
				}),

			new RouteDefinitionDTO(
				RouteId: "GetPlatformLogByIdEntryPoint",
				MatchPath: "/platform-logging/logs/{id}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new[] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathPattern", "/platform-logging/logs/{id}" }
				})
		};
	}

	public IEnumerable<ClusterDefinitionDTO> GetClusters()
	{
		return Enumerable.Empty<ClusterDefinitionDTO>();
	}
}
