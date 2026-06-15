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

			if (string.IsNullOrEmpty(response))
			{
				return string.Empty;
			}

			return response;
		}

		public async Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO)
		{
			var request = new { emailInvitationRequestDTO };

			var response = await _httpClient.PostAsJsonAsync("ats/insertemailinvitationrequest", request);

			var successContentInfo = await response.Content.ReadFromJsonAsync<bool>();

			return successContentInfo;
		}

		public async Task<bool> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetails)
		{
			using var content = new MultipartFormDataContent();

				void AddString(string? value, string name)
				{
					if (!string.IsNullOrWhiteSpace(value))
					{
						content.Add(new StringContent(value), name);
					}
				}

				void AddFile(byte[]? file, string name)
				{
					if (file != null)
					{
						var stream = new MemoryStream(file);
						var fileContent = new StreamContent(stream);
						fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
						content.Add(fileContent, name, name);
					}
				}

				AddString(bulkUploadFileDetails.PackageType, "bulkUploadFileDetailsDTO.PackageType");
				AddString(bulkUploadFileDetails.OrderType, "bulkUploadFileDetailsDTO.OrderType");
				AddString(bulkUploadFileDetails.FileName, "bulkUploadFileDetailsDTO.FileName");
				AddFile(bulkUploadFileDetails.BulkFile, "bulkUploadFileDetailsDTO.BulkFile");

			var response = await _httpClient.PostAsync("ats/insertbulksubject", content);

			var successContentInfo = await response.Content.ReadFromJsonAsync<bool>();

			return successContentInfo;

		}
	}
}
