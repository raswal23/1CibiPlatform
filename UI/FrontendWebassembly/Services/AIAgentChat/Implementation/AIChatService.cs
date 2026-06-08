using Microsoft.AspNetCore.Components.Forms;

namespace FrontendWebassembly.Services.AIAgentChat.Implementation;

public class AIChatService : IAIAgentChatService
{
	private readonly string _userIdKey;
	private HttpClient _httpClient;
	private readonly LocalStorageService _localStorageService;
	private readonly ILogger<AuthService> _logger;
	private HubConnection? _hubConnection;

	// Public events for UI to subscribe
	public event Action<string>? AiResponseReceived;
	public event Action<bool>? TypingStatusChanged;

	public AIChatService(IHttpClientFactory httpClientFactory,
		LocalStorageService localStorageService,
		ILogger<AuthService> logger)
	{
		this._httpClient = httpClientFactory.CreateClient("API");
		this._localStorageService = localStorageService;
		this._logger = logger;

		this._userIdKey = "UserId";
	}

	public async Task<AIAnswerDTO> AskAIAsync(
		string question,
		IBrowserFile? file,
		string? explicitSkillName,
		CancellationToken cancellationToken)
	{
		// Resolve user id (adjust to your LocalStorageService API)
		var userId = await _localStorageService.GetItemAsync<string?>(_userIdKey) ?? Guid.CreateVersion7().ToString();

		await EnsureHubConnectionStartedAsync(userId, cancellationToken);

		var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
		void LocalHandler(string message) => tcs.TrySetResult(message);
		// Register cancellation so that when caller cancels, the waiting tcs is completed as canceled
		using var ctr = cancellationToken.Register(() => tcs.TrySetCanceled());
		try
		{
			// subscribe to centralized event (registered in EnsureHubConnectionStartedAsync)
			AiResponseReceived += LocalHandler;

			// Trigger server processing via REST endpoint with optional file upload
			HttpResponseMessage httpResponse;

			// Check if file is attached - use multipart/form-data for file upload
			if (file != null)
			{
				using var content = new MultipartFormDataContent();


				// Add userId, question, and explicitSkillName as form fields
				content.Add(new StringContent(userId), "UserId");
				content.Add(new StringContent(question), "Question");
				content.Add(new StringContent(explicitSkillName ?? string.Empty), "ExplicitSkillName");


				// Add file stream content (max 512MB as per your API limit)
				var fileStream = file.OpenReadStream(maxAllowedSize: 512 * 1024 * 1024);
				var streamContent = new StreamContent(fileStream);
				streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
				content.Add(streamContent, "file", file.Name);

				// Post multipart form data to API
				httpResponse = await _httpClient.PostAsync("aiagent/ask", content, cancellationToken);
			}
			else
			{
				// No file - use JSON with explicitSkillName
				var req = new { UserId = userId, Question = question, ExplicitSkillName = explicitSkillName ?? string.Empty };
				httpResponse = await _httpClient.PostAsJsonAsync("aiagent/ask", req, cancellationToken);
			}

			if (!httpResponse.IsSuccessStatusCode)
			{
				_logger.LogWarning("AI ask request failed with status code {StatusCode}", httpResponse.StatusCode);

				var errorContent = await httpResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();

				_logger.LogError("AI ask error detail: {Detail}", errorContent?.Detail);
				return new AIAnswerDTO(new List<string>(), null, errorContent?.Detail ?? "");
			}

			var httpDto = await httpResponse.Content.ReadFromJsonAsync<AIAnswerDTO>(cancellationToken: cancellationToken)
				?? new AIAnswerDTO(new List<string>(), null);

			// Wait for hub message with short timeout; fallback to HTTP response if hub doesn't arrive.
			var delayTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
			var completed = await Task.WhenAny(tcs.Task, delayTask);

			// If caller cancelled, tcs.Task will be in canceled state and awaiting it will throw OperationCanceledException.
			if (completed == tcs.Task)
			{
				var hubAnswer = await tcs.Task; // may throw if canceled
				return new AIAnswerDTO(new List<string> { hubAnswer ?? string.Empty }, httpDto.DownloadUrl, null);
			}

			// Fallback: return HTTP response answer(s)
			return httpDto;
		}
		finally
		{
			AiResponseReceived -= LocalHandler;
		}
	}

	private async Task EnsureHubConnectionStartedAsync(string userId, CancellationToken cancellationToken)
	{
		if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
		{
			return;
		}

		// Build hub URL relative to API base. Adjust if different in your hosting setup.
		var baseUri = _httpClient.BaseAddress?.ToString()?.TrimEnd('/') ?? string.Empty;
		var hubUrl = $"{baseUri}/hubs/aiagent?userId={Uri.EscapeDataString(userId)}";

		_hubConnection = new HubConnectionBuilder()
			.WithUrl(hubUrl)
			.WithAutomaticReconnect()
			.Build();

		// Centralized handlers that raise public events
		_hubConnection.On<string>("ReceiveAiResponse", (message) =>
		{
			try
			{
				AiResponseReceived?.Invoke(message);
			}
			catch { }
		});

		_hubConnection.On<bool>("ReceiveTyping", (isTyping) =>
		{
			try
			{
				TypingStatusChanged?.Invoke(isTyping);
			}
			catch { }
		});

		_hubConnection.On("SessionCleared", () =>
		{
			try
			{
				TypingStatusChanged?.Invoke(false);
			}
			catch { }
		});

		_hubConnection.Closed += async (ex) =>
		{
			_logger.LogWarning(ex, "AI hub connection closed.");
			await Task.CompletedTask;
		};

		await _hubConnection.StartAsync(cancellationToken);
	}
}
