namespace FrontendWebassembly.Services.ATS.Implementation
{
	public class EndorsementSubmissionService : IEndorsementSubmissionService
	{
		private readonly string _userIdKey;
		private readonly HttpClient _httpClient;
		private readonly ILogger<EndorsementSubmissionService> _logger;
		private readonly LocalStorageService _localStorageService;
		private HubConnection? _hubConnection;

		public event Action<string>? ATSResponseReceived;

		public EndorsementSubmissionService(
			IHttpClientFactory httpClientFactory,
			ILogger<EndorsementSubmissionService> logger,
			LocalStorageService localStorageService)
		{
			_httpClient = httpClientFactory.CreateClient("API");
			_logger = logger;
			_localStorageService = localStorageService;
			_userIdKey = "UserId";
		}

		public async Task StartAsync()
		{
			if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
			{
				return;
			}
			var userId = await _localStorageService.GetItemAsync<string?>(_userIdKey) ?? Guid.CreateVersion7().ToString();
			var baseUri = _httpClient.BaseAddress?.ToString()?.TrimEnd('/') ?? string.Empty;
			var hubUrl = $"{baseUri}/hubs/atsbulk?userId={userId}";

			_hubConnection = new HubConnectionBuilder()
				.WithUrl(hubUrl)
				.WithAutomaticReconnect()
				.Build();

			_hubConnection.On<string>("ReceiveATSResponse", (message) =>
			{
				try
				{
					ATSResponseReceived?.Invoke(message);
				}
				catch { }
			});

			_hubConnection.On("SessionCleared", () =>
			{
				try
				{
					ATSResponseReceived?.Invoke(string.Empty);
				}
				catch { }
			});

			_hubConnection.Closed += async (ex) =>
			{
				_logger.LogWarning(ex, "ATS hub connection closed.");
				await Task.CompletedTask;
			};

			await _hubConnection.StartAsync();
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

				void AddFile(IBrowserFile? file, string name)
				{
					if (file != null)
					{
						var fileStream = file.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024);
						var fileContent = new StreamContent(fileStream);
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
