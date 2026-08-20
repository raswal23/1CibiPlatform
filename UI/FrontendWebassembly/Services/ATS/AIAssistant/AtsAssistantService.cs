namespace FrontendWebassembly.Services.ATS.AIAssistant;

public class AtsAssistantService : IAtsAssistantService
{
	private const string UserIdKey = "UserId";

	private readonly HttpClient _httpClient;
	private readonly LocalStorageService _localStorageService;
	private readonly ILogger<AtsAssistantService> _logger;
	private HubConnection? _hubConnection;

	public event Action<string>? ChatResponseReceived;

	public event Action<bool>? TypingChanged;

	public AtsAssistantService(
		IHttpClientFactory httpClientFactory,
		LocalStorageService localStorageService,
		ILogger<AtsAssistantService> logger)
	{
		_httpClient = httpClientFactory.CreateClient("API");
		_localStorageService = localStorageService;
		_logger = logger;
	}

	public async Task StartAsync()
	{
		if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
		{
			return;
		}

		var userId = await _localStorageService.GetItemAsync<string?>(UserIdKey);

		if (string.IsNullOrWhiteSpace(userId))
		{
			return;
		}

		var baseUri = _httpClient.BaseAddress?.ToString()?.TrimEnd('/') ?? string.Empty;
		var hubUrl = $"{baseUri}/hubs/atsbulk?userId={Uri.EscapeDataString(userId)}";

		_hubConnection = new HubConnectionBuilder()
			.WithUrl(hubUrl)
			.WithAutomaticReconnect()
			.Build();

		_hubConnection.On<string>("ReceiveChatResponse", message =>
		{
			ChatResponseReceived?.Invoke(message);
		});

		_hubConnection.On<bool>("ReceiveChatTyping", isTyping =>
		{
			TypingChanged?.Invoke(isTyping);
		});

		_hubConnection.Closed += async exception =>
		{
			_logger.LogWarning(exception, "ATS assistant hub connection closed.");
			await Task.CompletedTask;
		};

		await _hubConnection.StartAsync();
	}

	public async Task<AtsChatAnswerDTO> AskAsync(
		string question,
		CancellationToken cancellationToken = default)
	{
		var request = new { Question = question };

		var response = await _httpClient.PostAsJsonAsync(
			"ats/askassistant",
			request,
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var result = await response.Content.ReadFromJsonAsync<AskAtsAssistantResponseDTO>(
			cancellationToken: cancellationToken);

		return result!.Answer;
	}

	public async Task<AtsChatAnswerDTO> ConfirmOrderDraftAsync(
		Guid draftId,
		CancellationToken cancellationToken = default)
	{
		var request = new { DraftId = draftId };

		var response = await _httpClient.PostAsJsonAsync(
			"ats/confirmorderdraft",
			request,
			cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var result = await response.Content.ReadFromJsonAsync<AskAtsAssistantResponseDTO>(
			cancellationToken: cancellationToken);

		return result!.Answer;
	}

	public async Task<IReadOnlyList<AtsOrderSummaryDTO>> SearchOrdersBySubjectAsync(
		string name,
		CancellationToken cancellationToken = default)
	{
		var query = $"ats/searchordersbysubject?name={Uri.EscapeDataString(name)}";

		var response = await _httpClient.GetAsync(query, cancellationToken);

		await EnsureSuccessAsync(response, cancellationToken);

		var result = await response.Content.ReadFromJsonAsync<SearchOrdersBySubjectResponseDTO>(
			cancellationToken: cancellationToken);

		return result?.Orders ?? Array.Empty<AtsOrderSummaryDTO>();
	}

	private async Task EnsureSuccessAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

		ApiErrorResponse? error = null;

		try
		{
			error = JsonSerializer.Deserialize<ApiErrorResponse>(
				rawBody,
				new JsonSerializerOptions(JsonSerializerDefaults.Web));
		}
		catch (JsonException)
		{
			// Not valid JSON, so the raw body is preserved below instead.
		}

		var detail = string.IsNullOrWhiteSpace(error?.Detail) ? rawBody : error!.Detail;

		_logger.LogError(
			"ATS assistant request failed with {StatusCode}: {Detail} (TraceId {TraceId})",
			response.StatusCode,
			detail,
			error?.TraceId);

		throw new Exception(string.IsNullOrWhiteSpace(error?.TraceId)
			? detail
			: $"{detail}\nTraceId: {error!.TraceId}");
	}
}
