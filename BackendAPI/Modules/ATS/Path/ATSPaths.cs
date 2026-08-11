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
				RouteId: "GetATSDashboard",
				MatchPath: "/ats/getdashboard",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getdashboard" }
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

			new RouteDefinitionDTO(
				RouteId: "GetATSRoles",
				MatchPath: "/ats/getroles",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getroles" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddATSRole",
				MatchPath: "/ats/addrole",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/addrole" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditATSRole",
				MatchPath: "/ats/editrole",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/editrole" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetATSModules",
				MatchPath: "/ats/getmodules",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getmodules" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddATSModule",
				MatchPath: "/ats/addmodule",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/addmodule" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditATSModule",
				MatchPath: "/ats/editmodule",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/editmodule" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetATSUsers",
				MatchPath: "/ats/getusers",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getusers" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetATSAuthUsers",
				MatchPath: "/ats/getauthusers",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getauthusers" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetATSMyModules",
				MatchPath: "/ats/getmymodules",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getmymodules" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetMyRoleId",
				MatchPath: "/ats/getmyroleid",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getmyroleid" }
				}
			),
			

			new RouteDefinitionDTO(
				RouteId: "GetATSUserClientAssignments",
				MatchPath: "/ats/getuserclientassignments",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getuserclientassignments" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AssignATSUserClient",
				MatchPath: "/ats/assignuserclient",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/assignuserclient" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetATSClientAssignments",
				MatchPath: "/ats/getclientassignments",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getclientassignments" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetATSAssignableClients",
				MatchPath: "/ats/getassignableclients",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getassignableclients" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AssignATSClient",
				MatchPath: "/ats/assignclient",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Put },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/assignclient" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddATSUser",
				MatchPath: "/ats/adduser",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/adduser" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditATSUser",
				MatchPath: "/ats/edituser",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/edituser" }
				}
			),

		};
	}
	public IEnumerable<ClusterDefinitionDTO> GetClusters()
	{
		return Enumerable.Empty<ClusterDefinitionDTO>();
	}
}

