namespace BuildingBlocks.SharedConstants;

public static class GatewayConstants
{
	// Cluster IDs
	public const string OnePlatformApi = "onePlatformApi";
	public const string OnePlatformUI = "BlazorUI";
	public const string CTVIIntertalAPI = "CTVIIntertalAPI";

	// Rate limit policy names
	public static class RateLimitPolicies
	{
		public const string LoginPolicy = "LoginPolicy";
		public const string DefaultStrict = "DefaultStrict";
		public const string Default = "default";
	}

	// Http methods
	public static class HttpMethod
	{
		public const string Get = "GET";
		public const string Post = "POST";
		public const string Patch = "PATCH";
		public const string Delete = "DELETE";
	}
}
