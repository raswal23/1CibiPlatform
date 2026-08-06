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
				Methods: new [] { GatewayConstants.HttpMethod.Post },
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
					{ "PathSet", "/insertemailinvitationrequest" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetWithdrawnApplicationForm",
				MatchPath: "/ats/getwithdrawnapplicationforms",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getwithdrawnapplicationforms" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetDisputeOrders",
				MatchPath: "/ats/getdisputeorders",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getdisputeorders" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetPackages",
				MatchPath: "/ats/getpackages",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getpackages" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddPackage",
				MatchPath: "/ats/addpackage",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/addpackage" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditPackage",
				MatchPath: "/ats/editpackage",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/editpackage" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetReports",
				MatchPath: "/ats/getreports",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getreports" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetReportResult",
				MatchPath: "/ats/getreportresult",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getreportresult" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "MarkAsDisputed",
				MatchPath: "/ats/markasdisputed",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/markasdisputed" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "UploadReport",
				MatchPath: "/ats/uploadreport",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/uploadreport" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "DownloadIndividualReport",
				MatchPath: "/ats/downloadindividualreport",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/downloadindividualreport" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "DownloadMultipleRecords",
				MatchPath: "/ats/downloadmultipleorderrecords",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/downloadmultipleorderrecords" }
				}
			),
			
			new RouteDefinitionDTO(
				RouteId: "ResendApplicationForm",
				MatchPath: "/ats/resendapplicationform",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/resendapplicationform" }
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
			),

			new RouteDefinitionDTO(
				RouteId: "WithdrawnApplicationForm",
				MatchPath: "/ats/withdrawnapplicationform",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/withdrawnapplicationform" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetBulkInsertResponseEntryPoint",
				MatchPath: "/hubs/atsbulk/{**catch-all}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get, GatewayConstants.HttpMethod.Post}
			),

			new RouteDefinitionDTO(
				RouteId: "GetClients",
				MatchPath: "/ats/getclients",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getclients" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddClient",
				MatchPath: "/ats/addclient",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/addclient" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditClient",
				MatchPath: "/ats/editclient",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/editclient" }
				}
			),

		};
	}
	public IEnumerable<ClusterDefinitionDTO> GetClusters()
	{
		return Enumerable.Empty<ClusterDefinitionDTO>();
	}
}

