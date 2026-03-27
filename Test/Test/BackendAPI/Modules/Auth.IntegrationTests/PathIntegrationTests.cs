using FluentAssertions;
using System.Net;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.Auth.IntegrationTests;

public class PathIntegrationTests : BaseIntegrationTest
{
	private readonly HttpClient _client;

	public PathIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
		_client = factory.CreateClient();
	}

	[Theory]
	[InlineData("/login", "POST")]
	[InlineData("/loginweb", "POST")]
	[InlineData("/getnewaccesstoken", "POST")]
	[InlineData("/isauthenticated", "GET")]
	[InlineData("/register", "POST")]
	[InlineData("/verify/otp", "POST")]
	[InlineData("/verify/validate/otp", "POST")]
	[InlineData("/verify/resend-otp", "POST")]
	[InlineData("/forgot-password-email-send", "POST")]
	[InlineData("/is-change-password-token-valid", "POST")]
	[InlineData("/change-password", "POST")]
	[InlineData("/logout", "POST")]
	[InlineData("/auth/getusers", "GET")]
	[InlineData("/auth/getapplications", "GET")]
	[InlineData("/auth/addapplication", "POST")]
	[InlineData("/auth/editapplication", "PATCH")]
	[InlineData("/auth/deleteapplication/123", "DELETE")]
	[InlineData("/auth/getsubmenus", "GET")]
	[InlineData("/auth/addsubmenu", "POST")]
	[InlineData("/auth/editsubmenu", "PATCH")]
	[InlineData("/auth/deletesubmenu/123", "DELETE")]
	[InlineData("/auth/getappsubroles", "GET")]
	[InlineData("/auth/addappsubrole", "POST")]
	[InlineData("/auth/editappsubrole", "PATCH")]
	[InlineData("/auth/deleteappsubrole/123", "DELETE")]
	[InlineData("/auth/getroles", "GET")]
	[InlineData("/auth/addrole", "POST")]
	[InlineData("/auth/editrole", "PATCH")]
	[InlineData("/auth/deleterole/123", "DELETE")]
	[InlineData("/account/notification", "POST")]
	[InlineData("/sso/login/callback", "GET")]
	[InlineData("/sso/is-user-authenticated", "GET")]
	[InlineData("/sso/logout", "POST")]
	[InlineData("/Saml2", "POST")]
	[InlineData("/Saml2/Acs", "POST")]
	public async Task GatewayRoute_ShouldBeReachable_AndReturnValidHttpStatusCode(
		string path,
		string httpMethod)
	{
		// Arrange
		HttpRequestMessage request = new HttpRequestMessage(
			new HttpMethod(httpMethod),
			path
		);

		// Act
		HttpResponseMessage response = await _client.SendAsync(request);

		// Assert
		response.StatusCode.Should().NotBe(HttpStatusCode.BadGateway, "gateway should route successfully, not return 502");
		response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable, "gateway should route successfully, not return 503");
		response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, "route should exist and not return 404");
	}
}