using BuildingBlocks.SharedConstants;
using BuildingBlocks.SharedDTO;

namespace ApiGateways.YarpApiGateway.Config;

public static class OnePlatformConfigLoader
{
 private const string SourceClusterId = "onePlatform"; // value used in appsettings
 private static readonly string TargetClusterId = GatewayConstants.OnePlatformApi; // new cluster id

 // Reads ReverseProxy:Routes from configuration and adds routes that reference SourceClusterId
 public static void AddRoutesFromConfiguration(IConfiguration config, List<RouteDefinitionDTO> routeDtos, List<ClusterDefinitionDTO> clusterDtos)
 {
 var routesSection = config.GetSection("ReverseProxy:Routes");
 if (!routesSection.Exists())
 return;

 foreach (var routeSection in routesSection.GetChildren())
 {
 var clusterId = routeSection.GetValue<string>("ClusterId");
 if (!string.Equals(clusterId, SourceClusterId, StringComparison.OrdinalIgnoreCase))
 continue;

 var routeId = routeSection.Key;
 var matchPath = routeSection.GetSection("Match").GetValue<string>("Path") ?? string.Empty;
 var methods = routeSection.GetSection("Match").GetSection("Methods").Get<string[]>();

 IDictionary<string, string>? transforms = null;
 var transformsSection = routeSection.GetSection("Transforms");
 if (transformsSection.Exists())
 {
 // Transforms is an array of objects with single key/value each; merge into dictionary
 transforms = new Dictionary<string, string>();
 foreach (var t in transformsSection.GetChildren())
 {
 foreach (var kv in t.GetChildren())
 {
 transforms[kv.Key] = kv.Value ?? string.Empty;
 }
 }
 }

 var routeDto = new RouteDefinitionDTO(
 RouteId: routeId,
 MatchPath: matchPath,
 ClusterId: TargetClusterId,
 Methods: methods,
 Transforms: transforms,
 Metadata: null
 );

 routeDtos.Add(routeDto);
 }

 // Add cluster definition if not already present
 if (!clusterDtos.Any(c => string.Equals(c.ClusterId, TargetClusterId, StringComparison.OrdinalIgnoreCase)))
 {
 var cluster = new ClusterDefinitionDTO(
 ClusterId: TargetClusterId,
 Destinations: new[] { new DestinationDefinitionDTO(Id: "d1", Address: "http://apis:8080") }
 );
 clusterDtos.Add(cluster);
 }
 }
}
