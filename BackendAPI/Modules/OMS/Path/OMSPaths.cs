namespace OMS.Path;

/// <summary>
/// Public gateway routes for the OMS vertical slice.
/// </summary>
public sealed class OMSPaths : IReverseProxyModule
{
	public IEnumerable<RouteDefinitionDTO> GetRoutes() =>
	[
		new RouteDefinitionDTO(
			RouteId: "OMSCreateTicket",
			MatchPath: "/oms/createticket",
			ClusterId: GatewayConstants.OnePlatformApi,
			Methods: [GatewayConstants.HttpMethod.Post],
			Transforms: new Dictionary<string, string>
			{
				["PathSet"] = "/api/oms/tickets"
			})
	];

	public IEnumerable<ClusterDefinitionDTO> GetClusters() => [];
}
