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
					{ "PathSet", "/getemailidandapplicationformpath" }
				}
			),
			new RouteDefinitionDTO(
				RouteId: "InsertBulkSubject",
				MatchPath: "/ats/insertbulksubject",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/insertbulksubject" }
				}
			),
			new RouteDefinitionDTO(
				RouteId: "InsertEmailInvitationRequest",
				MatchPath: "/ats/insertemailinvitationrequest",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/insertEmailInvitationRequest" }
				}
			),
			new RouteDefinitionDTO(
				RouteId: "DownloadBulkTemplate",
				MatchPath: "/ats/downloadbulktemplate",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/downloadbulktemplate" }
				}
			)

		};
	}
	public IEnumerable<ClusterDefinitionDTO> GetClusters()
	{
		return Enumerable.Empty<ClusterDefinitionDTO>();
	}
}

