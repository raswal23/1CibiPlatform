namespace EmploymentVerification.Path;

/// <summary>
/// Public gateway routes for the Employment Verification vertical slice.
/// </summary>
public sealed class EmploymentVerificationPaths : IReverseProxyModule
{
	public IEnumerable<RouteDefinitionDTO> GetRoutes() =>
	[
		new RouteDefinitionDTO(
			RouteId: "GetEmploymentVerificationPreview",
			MatchPath: "/employmentverification/preview/{token}",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Get],
            // PathPattern (not PathSet) substitutes the {token} route value.
            // PathSet forwards the literal text "{token}" to the backend.
            Transforms: new Dictionary<string, string>
			{
				["PathPattern"] = "/api/employment-verification/preview/{token}"
			}),
		new RouteDefinitionDTO(
			RouteId: "GetEmploymentVerificationATSInProgress",
			MatchPath: "/employmentverification/getatsinprogress",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Get],
			Transforms: new Dictionary<string, string>
			{
				["PathSet"] = "/api/employment-verification/ats/in-progress"
			}),
		new RouteDefinitionDTO(
			RouteId: "GetEmploymentVerificationRequests",
			MatchPath: "/employmentverification/getrequests",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Get],
			Transforms: new Dictionary<string, string>
			{
				["PathSet"] = "/api/employment-verification/requests"
			}),
		new RouteDefinitionDTO(
			RouteId: "GetEmploymentVerificationSentRequests",
			MatchPath: "/employmentverification/getsentrequests",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Get],
			Transforms: new Dictionary<string, string>
			{
				["PathSet"] = "/api/employment-verification/requests/sent"
			}),
		new RouteDefinitionDTO(
			RouteId: "CreateEmploymentVerificationRequest",
			MatchPath: "/employmentverification/createrequest",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Post],
			Transforms: new Dictionary<string, string>
			{
				["PathSet"] = "/api/employment-verification/requests"
			}),
		new RouteDefinitionDTO(
			RouteId: "VerifyEmploymentVerificationRequest",
			MatchPath: "/employmentverification/verify/{token}",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Post],
			Transforms: new Dictionary<string, string>
			{
				["PathPattern"] = "/api/employment-verification/verify/{token}"
			}),
		new RouteDefinitionDTO(
			RouteId: "RejectEmploymentVerificationRequest",
			MatchPath: "/employmentverification/reject/{token}",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Post],
			Transforms: new Dictionary<string, string>
			{
				["PathPattern"] = "/api/employment-verification/reject/{token}"
			}),

		// Contact directory. The three share one backend path and are distinguished by
		// method, so they stay separate route entries - a single entry listing all
		// three methods would forward a PATCH to the GET handler.
		new RouteDefinitionDTO(
			RouteId: "GetEmploymentVerificationContacts",
			MatchPath: "/employmentverification/getcontacts",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Get],
			Transforms: new Dictionary<string, string>
			{
				["PathSet"] = "/api/employment-verification/contacts"
			}),
		new RouteDefinitionDTO(
			RouteId: "AddEmploymentVerificationContact",
			MatchPath: "/employmentverification/addcontact",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Post],
			Transforms: new Dictionary<string, string>
			{
				["PathSet"] = "/api/employment-verification/contacts"
			}),
		new RouteDefinitionDTO(
			RouteId: "EditEmploymentVerificationContact",
			MatchPath: "/employmentverification/editcontact",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Patch],
			Transforms: new Dictionary<string, string>
			{
				["PathSet"] = "/api/employment-verification/contacts"
			})
	];

	public IEnumerable<ClusterDefinitionDTO> GetClusters() => [];

}
