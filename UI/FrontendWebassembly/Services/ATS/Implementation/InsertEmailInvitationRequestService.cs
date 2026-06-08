namespace FrontendWebassembly.Services.ATS.Implementation;

public class InsertEmailInvitationRequestService : IInsertEmailInvitationRequestService
{
	private readonly HttpClient _httpClient;

	public InsertEmailInvitationRequestService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<Guid> InsertEmailInvitationRequest(EmailInvitationRequestDTO emailInvitationRequestDTO)
	{
		var response = await _httpClient.PostAsJsonAsync("ats/insertEmailInvitationRequest", emailInvitationRequestDTO);
		response.EnsureSuccessStatusCode();
		var result = await response.Content.ReadFromJsonAsync<Guid>();
		return result;
	}
}
