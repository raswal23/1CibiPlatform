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

	private readonly List<ChatMessage> _messages = new();

	private string _currentMessage = string.Empty;
	private bool _isSending;
	private bool _isConfirming;
	private bool _isTyping;
	private bool _shouldScroll;
	private ElementReference _streamRef;
	private CancellationTokenSource? _cts;

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
		if (!_shouldScroll)
			return;

		_shouldScroll = false;

		await ScrollToBottomAsync();
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

		var question = _currentMessage.Trim();

		_currentMessage = string.Empty;
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

	private void ClearConversation()
	{
		_messages.Clear();
		_isTyping = false;
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

	public void Dispose()
	{
		AssistantService.TypingChanged -= OnTypingChanged;
		_cts?.Dispose();
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
