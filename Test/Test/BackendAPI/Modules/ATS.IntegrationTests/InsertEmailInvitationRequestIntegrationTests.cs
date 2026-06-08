using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class InsertEmailInvitationRequestIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
{
	private readonly HttpClient _client;

	public InsertEmailInvitationRequestIntegrationTests(IntegrationTestWebAppFactory factory)
	{
		_client = factory.CreateClient();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ReturnsCreatedAndEmailInvitationId()
	{
		var body = new
		{
			emailInvitationRequestDTO = new
			{
				LastName = "Doe",
				FirstName = "Jane",
				MiddleInitial = "A",
				EmailAddress = "jane.doe@example.com",
				MobileNumber = "+15555551234",
				SelectPackage = "Standard",
				RushNormal = "Normal"
			}
		};

		var response = await _client.PostAsJsonAsync("/insertEmailInvitationRequest", body);

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);

		var content = await response.Content.ReadAsStringAsync();
		var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});

		Assert.NotNull(doc);

		// Look for EmailInvitationID in the response (case-insensitive)
		var found = doc!.FirstOrDefault(kv => kv.Key.Equals("EmailInvitationID", StringComparison.OrdinalIgnoreCase));
		Assert.False(string.IsNullOrEmpty(found.Key), "Response did not contain EmailInvitationID");

		var idString = found.Value.GetString();
		Assert.True(Guid.TryParse(idString, out _), "EmailInvitationID is not a valid GUID");
	}
}
