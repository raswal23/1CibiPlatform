namespace ATS.Path;

public class ATSPaths : IReverseProxyModule
{
	public IEnumerable<RouteDefinitionDTO> GetRoutes()
	{
		return new[]
		{
			new RouteDefinitionDTO(
				RouteId: "AddApplicationFormDataEntryPoint",
				MatchPath: "/ats/addapplicationformdata",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/addapplicationformdata" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetEmailIdandApplicationFormPathEntryPoint",
				MatchPath: "/ats/getemailidandapplicationformpath",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/addapplicationformdata" }
				}
			)
		};
	}
	public IEnumerable<ClusterDefinitionDTO> GetClusters()
	{
		return Enumerable.Empty<ClusterDefinitionDTO>();
	}
}

