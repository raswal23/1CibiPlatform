using Markdig;

namespace FrontendWebassembly.Pages.AIAgentChat;

public partial class AIChat
{
	// ChatMessage model with file and download URL support
	class ChatMessage
	{
		public string Sender { get; init; } = string.Empty;
		public string Text { get; init; } = string.Empty;
		public string Html { get; init; } = string.Empty;
		public DateTime Time { get; init; } = DateTime.UtcNow;
		public string? FileName { get; init; } = null; // File name if message has attachment
		public string? DownloadUrl { get; init; } = null; // Download URL if AI returned a file
	}

	// UI State Properties
	private List<ChatMessage> Messages { get; set; } = new();
	private string CurrentMessage { get; set; } = string.Empty;
	private bool IsTyping { get; set; }
	private bool isSending;
	private CancellationTokenSource? _cts;
	private bool _currentRequestActive = false; // indicates UI is awaiting an AI response for the latest request

	// File Upload Properties
	private IBrowserFile? SelectedFile { get; set; } = null; // Currently selected file
	private bool IsDragging { get; set; } = false; // Drag & drop state
	private ElementReference _chatWindowRef; // Reference to chat window for auto-scroll

	// Skill Selection Property
	private string SelectedSkill { get; set; } = string.Empty; // Selected AI skill (empty = auto-select)

	// Cache API base URL for WASM performance
	private string? _apiBaseUrl;

	// Reuse a Markdown pipeline with advanced extensions and emoji support
	private static readonly MarkdownPipeline _mdPipeline =
		new MarkdownPipelineBuilder()
			.UseAdvancedExtensions()
			.UseEmojiAndSmiley()
			.UsePipeTables()
			.UseTaskLists()
			.UseGenericAttributes()
			.Build();

	// Helper method to build full download URL from relative path
	private string GetFullDownloadUrl(string? relativeUrl)
	{
		if (string.IsNullOrEmpty(relativeUrl))
			return string.Empty;

		// If the URL is already absolute, return as-is
		if (relativeUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
			relativeUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			return relativeUrl;
		}

		// Get cached API base URL (initialized in OnInitializedAsync)
		var apiBaseUrl = _apiBaseUrl ?? string.Empty;

		// Combine API base with relative path (remove leading slash from relative path if present)
		var cleanPath = relativeUrl.TrimStart('/');
		return $"{apiBaseUrl}/{cleanPath}";
	}

	protected override async Task OnInitializedAsync()
	{
		// Cache API base URL from configuration (WASM optimization)
		_apiBaseUrl = Configuration["ApiBase"]?.TrimEnd('/');

		// subscribe to realtime events
		AIChatService.TypingStatusChanged += OnTypingStatusChanged;
		AIChatService.AiResponseReceived += OnAiResponseReceived;

		await base.OnInitializedAsync();
	}

	// Handle typing indicator changes from SignalR
	private void OnTypingStatusChanged(bool isTyping)
	{
		IsTyping = isTyping;
		InvokeAsync(StateHasChanged);
	}

	// Handle AI response received from SignalR with download URL support
	private void OnAiResponseReceived(string message)
	{
		// Only handle hub responses when there is an active request (not cancelled)
		if (!_currentRequestActive)
			return;

		// convert markdown to html for assistant message using pipeline (supports emoji, tables, code fences, etc.)
		var html = Markdown.ToHtml(message ?? string.Empty, _mdPipeline);

		// Get the last request to check if it had a download URL
		string? downloadUrl = null;
		// Note: Download URL will be set later when AskAIAsync returns

		Messages.Add(
			new ChatMessage
			{
				Sender = "Assistant",
				Text = message ?? string.Empty,
				Html = html,
				DownloadUrl = downloadUrl,
				Time = DateTime.Now
			});
		// mark request completed so stray later hub messages are ignored
		_currentRequestActive = false;
		IsTyping = false;
		InvokeAsync(StateHasChanged);
	}

	// Send message on Enter key (Shift+Enter for new line)
	private async Task SendMessage(KeyboardEventArgs e)
	{
		// Send on Enter, allow Shift+Enter for new line
		if (e.Key != "Enter" || e.ShiftKey)
			return;

		await SendMessageInternal();
	}

	// Send message on button click
	private async Task SendMessageClick()
	{
		await SendMessageInternal();
	}

	// Internal method to send message with optional file
	private async Task SendMessageInternal()
	{
		if (isSending)
			return;

		// Require either message text or file
		if (string.IsNullOrWhiteSpace(CurrentMessage) && SelectedFile == null)
			return;

		var text = string.IsNullOrWhiteSpace(CurrentMessage) ? "(File attached)" : CurrentMessage.Trim();
		var fileName = SelectedFile?.Name;
		var fileToSend = SelectedFile;

		// Add user message to chat
		Messages.Add(new ChatMessage
		{
			Sender = "User",
			Text = text,
			Html = System.Net.WebUtility.HtmlEncode(text),
			FileName = fileName,
			Time = DateTime.Now
		});

		// Clear input fields
		CurrentMessage = string.Empty;
		SelectedFile = null;

		isSending = true;
		IsTyping = true;

		// create a new CTS for this request
		_cts?.Dispose();
		_cts = new CancellationTokenSource();
		_currentRequestActive = true; // mark this request as active

		StateHasChanged();

		try
		{
			// Pass the cancellation token, file, and selected skill to the service
			var skillToUse = string.IsNullOrWhiteSpace(SelectedSkill) ? null : SelectedSkill;
			var result = await AIChatService.AskAIAsync(text, fileToSend, skillToUse, _cts.Token);


			if (!string.IsNullOrEmpty(result.ErrorMessage))
			{
				var errorHtml = Markdown.ToHtml("**Error:** " + result.ErrorMessage, _mdPipeline);
				Messages.Add(new ChatMessage
				{
					Sender = "Assistant",
					Text = "Error: " + result.ErrorMessage,
					Html = errorHtml,
					Time = DateTime.Now
				});
				_currentRequestActive = false;
				return;
			}

			// If the service returned a result with download URL, update the last AI message
			// If download URL is present, we may need to update the last message
			if (!string.IsNullOrEmpty(result.DownloadUrl))
			{
				// Find the last assistant message and update it with download URL
				var lastAssistantMsg = Messages.LastOrDefault(m => m.Sender == "Assistant");
				if (lastAssistantMsg != null)
				{
					// Remove and re-add with download URL

					Messages.Remove(lastAssistantMsg);

					Messages.Add(new ChatMessage
					{
						Sender = lastAssistantMsg.Sender,
						Text = lastAssistantMsg.Text,
						Html = lastAssistantMsg.Html,
						DownloadUrl = result.DownloadUrl,
						Time = lastAssistantMsg.Time
					});
					_currentRequestActive = false;
					return;
				}
			}
		}
		catch (OperationCanceledException)
		{
			var canceledHtml = Markdown.ToHtml("**Request canceled.**", _mdPipeline);
			Messages.Add(new ChatMessage { Sender = "Assistant", Text = "Request canceled.", Html = canceledHtml, Time = DateTime.Now });
			_currentRequestActive = false;
		}
		catch (Exception ex)
		{
			var errorHtml = Markdown.ToHtml("**Error:** " + ex.Message, _mdPipeline);
			Messages.Add(new ChatMessage { Sender = "Assistant", Text = "Error: " + ex.Message, Html = errorHtml, Time = DateTime.Now });
			_currentRequestActive = false;
		}
		finally
		{
			isSending = false;
			IsTyping = false;
			_cts?.Dispose();
			_cts = null;
			StateHasChanged();
		}
	}

	// Cancel ongoing AI request
	private void CancelRequest()
	{
		if (_cts?.IsCancellationRequested == false)
		{
			_cts.Cancel();
			IsTyping = false;
			isSending = false;
			_currentRequestActive = false; // ensure hub responses are ignored
			StateHasChanged();
		}
	}

	// Handle file selection from input dialog
	private async Task HandleFileSelected(InputFileChangeEventArgs e)
	{
		// Get the selected file (max 512MB as per API limit)
		SelectedFile = e.File;
		StateHasChanged();
	}

	// Trigger hidden file input click
	private async Task TriggerFileInput()
	{
		// Use JavaScript interop to trigger file input click
		await JS.InvokeVoidAsync("triggerFileInput", "fileInput");
	}

	// Remove selected file
	private void RemoveFile()
	{
		SelectedFile = null;
		StateHasChanged();
	}

	// Get appropriate icon based on file extension
	private string GetFileIcon(string fileName)
	{
		var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
		return extension switch
		{
			".pdf" => Icons.Material.Filled.PictureAsPdf,
			".doc" or ".docx" => Icons.Material.Filled.Description,
			".xls" or ".xlsx" => Icons.Material.Filled.TableChart,
			".jpg" or ".jpeg" or ".png" or ".gif" => Icons.Material.Filled.Image,
			".zip" or ".rar" => Icons.Material.Filled.FolderZip,
			".txt" => Icons.Material.Filled.TextSnippet,
			".csv" => Icons.Material.Filled.TableView,
			_ => Icons.Material.Filled.InsertDriveFile
		};
	}

	// Format file size for display
	private string FormatFileSize(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB" };
		double len = bytes;
		int order = 0;
		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len = len / 1024;
		}
		return $"{len:0.##} {sizes[order]}";
	}

	// Cleanup on component disposal
	public void Dispose()
	{
		AIChatService.TypingStatusChanged -= OnTypingStatusChanged;
		AIChatService.AiResponseReceived -= OnAiResponseReceived;
		_cts?.Dispose();
	}
}
