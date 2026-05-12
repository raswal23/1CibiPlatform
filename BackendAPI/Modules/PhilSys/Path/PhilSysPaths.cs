namespace BackendAPI.Modules.PhilSys.Path;

// This class provides route and cluster declarations for the Auth module.
// Comments explain how to use Metadata and Methods:
// - Use `Methods` to restrict allowed HTTP methods on the gateway (e.g. new[] { "POST" }).
// - Use `Metadata` to attach route-specific values such as rate-limit policy names or flags.
// Example: Metadata = new Dictionary<string,string> { { "RateLimitPolicy", "LoginPolicy" } }
// The gateway will read these DTOs at startup and convert them to YARP route/cluster configs.

public class PhilSysPaths : IReverseProxyModule
{
	public IEnumerable<RouteDefinitionDTO> GetRoutes()
	{
		return new[]
		{
			new RouteDefinitionDTO(
				RouteId: "PartnerSystemQueryEntryPoint",
				MatchPath: "/philsys/idv",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/partnersystemquery" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "UpdateFaceLivenessSessionEntryPoint",
				MatchPath: "/philsys/idv/updatefacelivenesssession",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/updatefacelivenesssession" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetLivenessSessionStatusEntryPoint",
				MatchPath: "/philsys/idv/validate/liveness",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/idv/validate/liveness" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "DeleteExpiredTransactionEntryPoint",
				MatchPath: "/philsys/deletetransaction/{HashToken}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Delete },
				Transforms: new Dictionary<string, string>
				{
					{ "PathRemovePrefix", "/philsys" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetLivenessSDKKeyEntryPoint",
				MatchPath: "/philsys/idv/getlivenesskey",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/philsys/getlivenesskey" }
				}
			)
		};
	}
	public IEnumerable<ClusterDefinitionDTO> GetClusters()
	{
		return Enumerable.Empty<ClusterDefinitionDTO>();
	}
}

