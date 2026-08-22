namespace FrontendWebassembly.Services.ATS.EndorsementSubmission;

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
		var baseUri = _httpClient.BaseAddress?.ToString()?.TrimEnd('/') ?? string.Empty;

		// No ?userId= any more. The hub derives the group from the authenticated
		// principal; the auth cookie rides along on the handshake automatically.
		var hubUrl = $"{baseUri}/hubs/atsbulk";

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
			catch (Exception ex)
			{
				// A subscriber that throws must not tear down the hub connection, but
				// swallowing it silently is how duplicate-notification bugs stay hidden.
				_logger.LogError(ex, "An ATS hub subscriber threw while handling a response.");
			}
		});

		_hubConnection.On("SessionCleared", () =>
		{
			try
			{
				ATSResponseReceived?.Invoke(string.Empty);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An ATS hub subscriber threw while handling SessionCleared.");
			}
		});

		_hubConnection.Closed += async (ex) =>
		{
			_logger.LogWarning(ex, "ATS hub connection closed.");
			await Task.CompletedTask;
		};

		await _hubConnection.StartAsync();
	}

	public async ValueTask DisposeAsync()
	{
		if (_hubConnection is not null)
		{
			await _hubConnection.DisposeAsync();
			_hubConnection = null;
		}
	}

	public async Task<ServiceResponse<string>> DownloadBulkTemplateAsync()
	{
		try
		{
			var response = await _httpClient.GetAsync("ats/downloadbulktemplate");

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<string>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<string>();

			if (string.IsNullOrWhiteSpace(result))
			{
				return ServiceResponse<string>.Failure("The server did not return a template download link.");
			}

			return ServiceResponse<string>.Success(result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<string>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<bool>> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO)
	{
		var request = new { emailInvitationRequestDTO };

		try
		{
			var response = await _httpClient.PostAsJsonAsync("ats/insertemailinvitationrequest", request);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<bool>.Failure(await response.ReadErrorDetailAsync());
			}

			var successContentInfo = await response.Content.ReadFromJsonAsync<bool>();

			return ServiceResponse<bool>.Success(successContentInfo);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<bool>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<bool>> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetails)
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
				content.Add(fileContent, name, file.Name);
			}
		}

		AddString(bulkUploadFileDetails.PackageType, "bulkUploadFileDetailsDTO.PackageType");
		AddString(bulkUploadFileDetails.OrderType, "bulkUploadFileDetailsDTO.OrderType");
		AddString(bulkUploadFileDetails.FileName, "bulkUploadFileDetailsDTO.FileName");
		AddFile(bulkUploadFileDetails.BulkFile, "bulkUploadFileDetailsDTO.BulkFile");

		try
		{
			var response = await _httpClient.PostAsync("ats/insertbulksubject", content);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<bool>.Failure(await response.ReadErrorDetailAsync());
			}

			var successContentInfo = await response.Content.ReadFromJsonAsync<bool>();

			return ServiceResponse<bool>.Success(successContentInfo);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<bool>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<KeysetPaginatedResult<EmailInvitationRequestListDTO>>> GetWithdrawnEmailInvitationRequestsAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null)
	{
		var query = $"ats/getwithdrawnapplicationforms?pageSize={pageSize}";
		if (!string.IsNullOrEmpty(cursor))
			query += $"&cursor={Uri.EscapeDataString(cursor)}";
		if (!string.IsNullOrEmpty(SearchTerm))
			query += $"&searchTerm={Uri.EscapeDataString(SearchTerm)}";

		try
		{
			var response = await _httpClient.GetAsync(query);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<KeysetPaginatedResult<EmailInvitationRequestListDTO>>.Failure(await response.ReadErrorDetailAsync());
			}

			var result = await response.Content.ReadFromJsonAsync<GetWithdrawnEmailInvitationRequestsResponseDTO>();

			if (result?.Requests is null)
			{
				return ServiceResponse<KeysetPaginatedResult<EmailInvitationRequestListDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<KeysetPaginatedResult<EmailInvitationRequestListDTO>>.Success(result.Requests);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<KeysetPaginatedResult<EmailInvitationRequestListDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<bool>> ResendApplicationFormAsync(Guid emailInvitationId)
	{
		var request = new { emailInvitationId };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync("ats/resendapplicationform", request);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<bool>.Failure(await response.ReadErrorDetailAsync());
			}

			var successContent = await response.Content.ReadFromJsonAsync<bool>();

			return ServiceResponse<bool>.Success(successContent);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<bool>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
