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

		/// <summary>
		/// The candidate-facing ATS application form: token lookup, submission and
		/// withdrawal. These accept unauthenticated callers, so they are not bounded by
		/// the login flow the way the rest of the platform is.
		/// </summary>
		public const string AnonymousApplicationForm = "AnonymousApplicationForm";
	}

	// Http methods
	public static class HttpMethod
	{
		public const string Get = "GET";
		public const string Post = "POST";
		public const string Put = "PUT";
		public const string Patch = "PATCH";
		public const string Delete = "DELETE";
	}
}
