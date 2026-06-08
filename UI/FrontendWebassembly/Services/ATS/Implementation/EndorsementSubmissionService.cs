namespace FrontendWebassembly.Services.ATS.Implementation
{
	public class EndorsementSubmissionService : IEndorsementSubmissionService
	{
		private readonly HttpClient _httpClient;


		public EndorsementSubmissionService(IHttpClientFactory httpClientFactory)
		{
			_httpClient = httpClientFactory.CreateClient("API");
		}

		public async Task<string> DownloadBulkTemplateAsync()
		{
			var response = await _httpClient.GetFromJsonAsync<string>("ats/downloadbulktemplate");
			if (string.IsNullOrEmpty(response)) {
				return string.Empty;
			}
			Console.WriteLine(response);
			return response;
		}

		public async Task<bool> InsertEmailInvitationRequest(EmailInvitationRequestDTO emailInvitationRequestDTO)
		{
			var response = await _httpClient.PostAsJsonAsync("ats/insertEmailInvitationRequest", emailInvitationRequestDTO);
			response.EnsureSuccessStatusCode();
			var result = true;
			return result;
		}
	}
}
