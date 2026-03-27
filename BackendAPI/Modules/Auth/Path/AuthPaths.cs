namespace Auth.Path;

// This class provides route and cluster declarations for the Auth module.
// Comments explain how to use Metadata and Methods:
// - Use `Methods` to restrict allowed HTTP methods on the gateway (e.g. new[] { "POST" }).
// - Use `Metadata` to attach route-specific values such as rate-limit policy names or flags.
// Example: Metadata = new Dictionary<string,string> { { "RateLimitPolicy", "LoginPolicy" } }
// The gateway will read these DTOs at startup and convert them to YARP route/cluster configs.
public class AuthPaths : IReverseProxyModule
{
	public IEnumerable<RouteDefinitionDTO> GetRoutes()
	{
		return new[]
		{
			new RouteDefinitionDTO(
				RouteId: "Auth_Login",
				MatchPath: "/token/generatetoken",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/login" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "LoginWebEntryPoint",
				MatchPath: "/token/web/generatetoken",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/loginweb" }
				},
				Metadata: new Dictionary<string,string>
				{
					{ "RateLimitPolicy", GatewayConstants.RateLimitPolicies.LoginPolicy }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "LoginWebRefreshTokenEntryPoint",
				MatchPath: "/token/web/getnewaccesstoken",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/getnewaccesstoken" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "IsAuthenticatedEntryPoint",
				MatchPath: "/auth/isAuthenticated",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/isauthenticated" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "RegisterEntryPoint",
				MatchPath: "/auth/register",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/register" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "VerifyOTPEntryPoint",
				MatchPath: "/auth/verify/otp",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/verify/otp" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "IsVerifiedOTPEntryPoint",
				MatchPath: "/auth/validate/otp",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/verify/validate/otp" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "ResendOTPEntryPoint",
				MatchPath: "/auth/resend-otp",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/verify/resend-otp" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "ForgotPasswordGetUserIdEntryPoint",
				MatchPath: "auth/forgot-password-email-send",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/forgot-password-email-send" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "ForgotPasswordIsTokenValidEntryPoint",
				MatchPath: "/auth/forgot-password/is-change-password-token-valid",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/is-change-password-token-valid" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "ForgotPasswordChangePasswordEntryPoint",
				MatchPath: "/auth/forgot-password/change-password",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/change-password" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "LogoutWebEntryPoint",
				MatchPath: "/auth/logout",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "/logout" }
				}
			),

			// Auth management endpoints
			new RouteDefinitionDTO(
				RouteId: "GetUsersEntryPoint",
				MatchPath: "auth/getusers",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/getusers" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetUnApprovedUsersEntryPoint",
				MatchPath: "auth/getunapprovedusers",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/getunapprovedusers" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditUserEntryPoint",
				MatchPath: "auth/edituser",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/edituser" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "SendApprovalNotificationEntryPoint",
				MatchPath: "account/approvalnotification",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "account/approvalnotification" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetApplicationsEntryPoint",
				MatchPath: "auth/getapplications",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/getapplications" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddApplicationEntryPoint",
				MatchPath: "auth/addapplication",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/addapplication" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditApplicationEntryPoint",
				MatchPath: "auth/editapplication",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/editapplication" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "DeleteApplicationEntryPoint",
				MatchPath: "auth/deleteapplication/{AppId}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Delete },
				Transforms: new Dictionary<string, string>
				{
					{ "PathRemovePrefix", "auth/" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetSubMenusEntryPoint",
				MatchPath: "auth/getsubmenus",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/getsubmenus" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddSubMenuEntryPoint",
				MatchPath: "auth/addsubmenu",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/addsubmenu" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditSubMenuEntryPoint",
				MatchPath: "auth/editsubmenu",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/editsubmenu" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "DeleteSubMenuEntryPoint",
				MatchPath: "auth/deletesubmenu/{SubMenuId}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Delete },
				Transforms: new Dictionary<string, string>
				{
					{ "PathRemovePrefix", "auth/" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetUserAppSubRoleEntryPoint",
				MatchPath: "auth/getappsubroles",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/getappsubroles" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddUserAppSubRoleEntryPoint",
				MatchPath: "auth/addappsubrole",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/addappsubrole" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditUserAppSubRoleEntryPoint",
				MatchPath: "auth/editappsubrole",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/editappsubrole" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "DeleteUserAppSubRoleEntryPoint",
				MatchPath: "auth/deleteappsubrole/{AppSubRoleId}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Delete },
				Transforms: new Dictionary<string, string>
				{
					{ "PathRemovePrefix", "auth/" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetRolesEntryPoint",
				MatchPath: "auth/getroles",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/getroles" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "AddRoleEntryPoint",
				MatchPath: "auth/addrole",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/addrole" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "EditRoleEntryPoint",
				MatchPath: "auth/editrole",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Patch },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/editrole" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "DeleteRoleEntryPoint",
				MatchPath: "auth/deleterole/{RoleId}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Delete },
				Transforms: new Dictionary<string, string>
				{
					{ "PathRemovePrefix", "auth/" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "SendNotificationEntryPoint",
				MatchPath: "account/notification",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Post },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "account/notification" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "GetLockedUserEntryPoint",
				MatchPath: "auth/getlockedusers",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Get },
				Transforms: new Dictionary<string, string>
				{
					{ "PathSet", "auth/getlockedusers" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "DeleteLockedUserEntryPoint",
				MatchPath: "auth/deletelockeduser/{lockUserId}",
				ClusterId: GatewayConstants.OnePlatformApi,
				Methods: new [] { GatewayConstants.HttpMethod.Delete },
				Transforms: new Dictionary<string, string>
				{
					{ "PathRemovePrefix", "auth/" }
				}
			),

			new RouteDefinitionDTO(
				RouteId: "FrontEndEntryPoint",
				MatchPath: "/{**catchall}",
				ClusterId: GatewayConstants.OnePlatformUI
			)
		};
	}

	public IEnumerable<ClusterDefinitionDTO> GetClusters()
	{
		return new[]
		{
			new ClusterDefinitionDTO(
				ClusterId: GatewayConstants.OnePlatformApi,
				Destinations: new []
				{
					new DestinationDefinitionDTO(
						Id: "d1",
						Address: "http://apis:8080"
					)
				}
			),
			new ClusterDefinitionDTO(
				ClusterId: GatewayConstants.OnePlatformUI,
				Destinations: new []
				{
					new DestinationDefinitionDTO(
						Id: "d1",
						Address: "http://frontendwebassembly:8080"
					)
				}
			)
		};
	}
}
