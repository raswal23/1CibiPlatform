using Markdig;

namespace FrontendWebassembly.Component.ATS;

public partial class AIAssistantComponent
{
	private static readonly string[] Suggestions =
	[
		"What is the status of Russel Gutierrez's order?",
		"Show me the orders for Dela Cruz",
		"Create a new order"
	];

	private static readonly MarkdownPipeline MarkdownPipeline =
		new MarkdownPipelineBuilder()
			.UseAdvancedExtensions()
			.UseEmojiAndSmiley()
			.UsePipeTables()
			.UseTaskLists()
			.Build();

	private const string DictationModulePath = "./js/ats/voiceDictation.js";

	private readonly List<ChatMessage> _messages = new();

	private string _currentMessage = string.Empty;
	private bool _isSending;
	private bool _isConfirming;
	private bool _isTyping;
	private bool _shouldScroll;
	private ElementReference _streamRef;
	private CancellationTokenSource? _cts;

	private IJSObjectReference? _dictationModule;
	private DotNetObjectReference<AIAssistantComponent>? _componentReference;
	private bool _isSpeechSupported;
	private bool _isListening;
	private string _dictationStatus = string.Empty;

	// The transcript confirmed so far. Interim words are appended to it for display only,
	// so the next result replaces the guess instead of duplicating it.
	private string _committedMessage = string.Empty;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		if (!IsPageAuthorized)
			return;

		AssistantService.TypingChanged += OnTypingChanged;

		try
		{
			await AssistantService.StartAsync();
		}
		catch (Exception exception)
		{
			// The chat still works over HTTP without the hub, so only the indicator is lost.
			Snackbar.Add(
				$"Live updates are unavailable: {exception.Message}",
				Severity.Info);
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
			await InitializeDictationAsync();

		if (!_shouldScroll)
			return;

		_shouldScroll = false;

		await ScrollToBottomAsync();
	}

	private async Task InitializeDictationAsync()
	{
		// No IsPageAuthorized guard here: SecurePageBase sets that flag after an await, so
		// the first render happens while it is still false. An unauthorized user has already
		// been redirected to /access-denied, and importing the module has no side effect.
		try
		{
			_dictationModule = await JS.InvokeAsync<IJSObjectReference>(
				"import",
				DictationModulePath);

			_isSpeechSupported = await _dictationModule.InvokeAsync<bool>("isSupported");
		}
		catch (Exception exception)
		{
			// Dictation is an enhancement. If the module or the browser API is missing the
			// composer simply stays keyboard-only, so this must not break the conversation.
			// It is logged rather than swallowed: a missing button is otherwise invisible.
			_isSpeechSupported = false;

			await LogDictationFailureAsync(exception.Message);
		}

		StateHasChanged();
	}

	private void OnTypingChanged(bool isTyping)
	{
		// While a request is in flight the local indicator already covers this, so the hub
		// signal only clears it.
		if (isTyping && _isSending)
			return;

		_isTyping = isTyping;

		InvokeAsync(StateHasChanged);
	}

	private async Task LogDictationFailureAsync(string reason)
	{
		try
		{
			await JS.InvokeVoidAsync(
				"console.warn",
				$"ATS assistant: voice dictation is unavailable — {reason}");
		}
		catch (JSException)
		{
			// Nothing more can be done from here.
		}
	}

	private async Task ToggleDictationAsync()
	{
		if (!_isSpeechSupported || _dictationModule is null)
			return;

		if (_isListening)
		{
			await StopDictationAsync();

			return;
		}

		_componentReference ??= DotNetObjectReference.Create(this);

		// Anything already in the box is kept, and dictation continues from it.
		_committedMessage = _currentMessage;

		bool started;

		try
		{
			started = await _dictationModule.InvokeAsync<bool>(
				"start",
				_componentReference,
				null);
		}
		catch (JSException)
		{
			started = false;
		}

		if (!started)
		{
			Snackbar.Add(
				"Dictation could not start. Check that this site is allowed to use your microphone.",
				Severity.Warning);

			return;
		}

		_isListening = true;
		_dictationStatus = "Listening.";

		StateHasChanged();
	}

	private async Task StopDictationAsync()
	{
		if (_dictationModule is null || !_isListening)
			return;

		_isListening = false;
		_dictationStatus = "Dictation stopped.";

		try
		{
			await _dictationModule.InvokeVoidAsync("stop");
		}
		catch (JSException)
		{
			// The recognizer was already torn down by the browser.
		}
		catch (JSDisconnectedException)
		{
			// The circuit is gone; nothing to release.
		}

		StateHasChanged();
	}

	[JSInvokable]
	public async Task OnSpeechResultAsync(string finalText, string interimText)
	{
		if (!string.IsNullOrWhiteSpace(finalText))
			_committedMessage = AppendTranscript(_committedMessage, finalText);

		_currentMessage = AppendTranscript(_committedMessage, interimText);

		await InvokeAsync(StateHasChanged);
	}

	[JSInvokable]
	public async Task OnSpeechErrorAsync(string code)
	{
		// A pause in speech is normal dictation behavior, and the browser restarts itself.
		if (code is "no-speech")
			return;

		// The user pressed the button; the stop is already reflected in the UI.
		if (code is not "aborted")
		{
			var (message, severity) = DescribeSpeechError(code);

			Snackbar.Add(message, severity);
		}

		_isListening = false;
		_dictationStatus = "Dictation stopped.";

		await InvokeAsync(StateHasChanged);
	}

	[JSInvokable]
	public async Task OnSpeechEndedAsync()
	{
		if (!_isListening)
			return;

		_isListening = false;
		_dictationStatus = "Dictation stopped.";

		await InvokeAsync(StateHasChanged);
	}

	// Typing while the mic is open must win, otherwise the next transcript would
	// overwrite the correction the user just made by hand.
	private void OnMessageTyped() =>
		_committedMessage = _currentMessage;

	private static (string Message, Severity Severity) DescribeSpeechError(string code) => code switch
	{
		"not-allowed" or "service-not-allowed" => (
			"Microphone access is blocked. Allow it in your browser's site settings to dictate.",
			Severity.Warning),
		"audio-capture" => (
			"No microphone was found.",
			Severity.Error),
		"network" => (
			"Speech recognition is offline right now.",
			Severity.Warning),
		_ => (
			"Dictation stopped unexpectedly. Please try again.",
			Severity.Warning)
	};

	private static string AppendTranscript(string existing, string addition)
	{
		var trimmedAddition = addition?.Trim();

		if (string.IsNullOrEmpty(trimmedAddition))
			return existing;

		return string.IsNullOrWhiteSpace(existing)
			? trimmedAddition
			: $"{existing.TrimEnd()} {trimmedAddition}";
	}

	private async Task HandleKeyDown(KeyboardEventArgs args)
	{
		if (args.Key != "Enter" || args.ShiftKey)
			return;

		await SendAsync();
	}

	private async Task UseSuggestionAsync(string suggestion)
	{
		_currentMessage = suggestion;

		await SendAsync();
	}

	private async Task SendAsync()
	{
		if (_isSending || string.IsNullOrWhiteSpace(_currentMessage))
			return;

		// Release the microphone before the request goes out, so a trailing transcript
		// cannot land in the box after the message was already sent.
		await StopDictationAsync();

		var question = _currentMessage.Trim();

		_currentMessage = string.Empty;
		_committedMessage = string.Empty;
		_isSending = true;
		_isTyping = true;

		AddMessage(ChatMessage.FromUser(question));

		_cts?.Dispose();
		_cts = new CancellationTokenSource();

		try
		{
			var answer = await AssistantService.AskAsync(question, _cts.Token);

			if (!string.IsNullOrWhiteSpace(answer.Error))
			{
				AddMessage(ChatMessage.FromAssistant(
					ToHtml($"**Sorry —** {answer.Error}")));

				return;
			}

			AddMessage(ChatMessage.FromAssistant(
				ToHtml(answer.Answer),
				answer.Orders,
				answer.PendingDraft));
		}
		catch (OperationCanceledException)
		{
			AddMessage(ChatMessage.FromAssistant(ToHtml("**Request cancelled.**")));
		}
		catch (Exception exception)
		{
			AddMessage(ChatMessage.FromAssistant(
				ToHtml($"**Sorry —** {exception.Message}")));
		}
		finally
		{
			_isSending = false;
			_isTyping = false;

			StateHasChanged();
		}
	}

	private async Task ConfirmDraftAsync(ChatMessage message)
	{
		if (message.Draft is null || _isConfirming)
			return;

		var confirmParam = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Submit Candidate"
			},
			{
				nameof(YesNoDialogComponent.Message),
				"Please be advised that this action will send an email invitation to your candidate."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Proceed"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"Clicking 'Proceed' will send an email invitation. Would you like to proceed?"
			}
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.ExtraSmall,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<YesNoDialogComponent>(null, confirmParam, options);
		var result = await dialog.Result;

		if (result?.Canceled != false)
			return;

		try
		{
			_isConfirming = true;

			var answer = await AssistantService.ConfirmOrderDraftAsync(message.Draft.DraftId);

			message.DraftState = OrderDraftState.Confirmed;

			AddMessage(ChatMessage.FromAssistant(ToHtml(answer.Answer)));

			Snackbar.Add("The order was created and the invitation is on its way.", Severity.Success);
		}
		catch (Exception exception)
		{
			Snackbar.Add(exception.Message, Severity.Error);
		}
		finally
		{
			_isConfirming = false;
		}
	}

	private void CancelDraft(ChatMessage message)
	{
		message.DraftState = OrderDraftState.Cancelled;

		Snackbar.Add("The draft order was discarded.", Severity.Info);
	}

	private async Task ClearConversation()
	{
		await StopDictationAsync();

		_messages.Clear();
		_isTyping = false;
		_committedMessage = string.Empty;
	}

	private void AddMessage(ChatMessage message)
	{
		_messages.Add(message);
		_shouldScroll = true;

		StateHasChanged();
	}

	private async Task ScrollToBottomAsync()
	{
		try
		{
			await JS.InvokeVoidAsync("atsAssistantScrollToBottom", _streamRef);
		}
		catch (JSException)
		{
			// Scrolling is cosmetic, so a missing helper must not break the conversation.
		}
	}

	private static string ToHtml(string? markdown) =>
		Markdown.ToHtml(markdown ?? string.Empty, MarkdownPipeline);

	private string GetMicClass() =>
		_isListening
			? "ats-assistant-mic is-listening"
			: "ats-assistant-mic";

	private static string GetRowClass(ChatMessage message) =>
		message.IsUser
			? "ats-assistant-row ats-assistant-row-user"
			: "ats-assistant-row ats-assistant-row-bot";

	private static string GetBubbleClass(ChatMessage message) =>
		message.IsUser
			? "ats-assistant-bubble ats-assistant-bubble-user"
			: "ats-assistant-bubble ats-assistant-bubble-bot";

	private static string GetStatusClass(string? orderStatus) => orderStatus switch
	{
		"Completed" => "ats-assistant-status ats-assistant-status-done",
		"In Progress" => "ats-assistant-status ats-assistant-status-active",
		"Application Withdrawn" => "ats-assistant-status ats-assistant-status-stopped",
		_ => "ats-assistant-status ats-assistant-status-waiting"
	};

	private static string GetDraftStateClass(OrderDraftState state) =>
		state == OrderDraftState.Confirmed
			? "ats-assistant-confirm-result is-confirmed"
			: "ats-assistant-confirm-result is-cancelled";

	private static string FormatDate(DateTime? value) =>
		value.HasValue ? value.Value.ToLocalTime().ToString("dd MMM yyyy") : "—";

	public async ValueTask DisposeAsync()
	{
		AssistantService.TypingChanged -= OnTypingChanged;
		_cts?.Dispose();

		if (_dictationModule is not null)
		{
			try
			{
				// Releases the microphone even if the user navigated away mid-sentence.
				await _dictationModule.InvokeVoidAsync("destroy");
				await _dictationModule.DisposeAsync();
			}
			catch (JSDisconnectedException)
			{
				// The browser context is already gone.
			}
			catch (JSException)
			{
				// The recognizer was torn down with the page.
			}

			_dictationModule = null;
		}

		_componentReference?.Dispose();
		_componentReference = null;
	}

	private enum OrderDraftState
	{
		Pending,
		Confirmed,
		Cancelled
	}

	private sealed class ChatMessage
	{
		public bool IsUser { get; init; }

		public string Html { get; init; } = string.Empty;

		public IReadOnlyList<AtsOrderSummaryDTO>? Orders { get; init; }

		public AtsOrderDraftDTO? Draft { get; init; }

		public OrderDraftState DraftState { get; set; } = OrderDraftState.Pending;

		public DateTime Time { get; init; } = DateTime.Now;

		public static ChatMessage FromUser(string text) => new()
		{
			IsUser = true,
			Html = System.Net.WebUtility.HtmlEncode(text)
		};

		public static ChatMessage FromAssistant(
			string html,
			IReadOnlyList<AtsOrderSummaryDTO>? orders = null,
			AtsOrderDraftDTO? draft = null) => new()
			{
				IsUser = false,
				Html = html,
				Orders = orders,
				Draft = draft
			};
	}
}
