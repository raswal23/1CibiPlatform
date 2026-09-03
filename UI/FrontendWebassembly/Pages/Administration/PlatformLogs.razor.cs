using FrontendWebassembly.DTO.Logging;
using System.Net;
using System.Text.Json;

namespace FrontendWebassembly.Pages.Administration;

public partial class PlatformLogs
{
	private readonly List<PlatformLogDTO> logs = [];
	private readonly string[] applications = ["ATS", "Auth", "PhilSys", "AIAgent", "SSO", "Platform"];
	private readonly string[] levels = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];
	private string? application;
	private string? level;
	private string? search;
	private string? nextCursor;
	private string? error;
	private bool isLoading;
	private PlatformLogDTO? selected;
	private string? copiedValue;

	private string? SelectedTime => selected?.OccurredAt.ToString("O");

	protected override Task OnInitializedAsync() => LoadFirstPageAsync();

	private async Task LoadFirstPageAsync()
	{
		logs.Clear();
		nextCursor = null;
		await LoadAsync(null);
	}

	private Task LoadMoreAsync() => LoadAsync(nextCursor);

	private async Task LoadAsync(string? cursor)
	{
		if (isLoading)
			return;

		isLoading = true;
		error = null;
		try
		{
			var result = await LogService.GetLogsAsync(
				DateTimeOffset.UtcNow.AddDays(-7),
				DateTimeOffset.UtcNow,
				application,
				level,
				search,
				cursor,
				50);

			logs.AddRange(result.Items);
			nextCursor = result.NextCursor;
		}
		catch (Exception exception)
		{
			error = $"Unable to load platform logs: {exception.Message}";
		}
		finally
		{
			isLoading = false;
		}
	}

	private void SelectLogOnKeyDown(KeyboardEventArgs args, PlatformLogDTO log)
	{
		if (args.Key is "Enter" or " ")
			selected = log;
	}

	private void CloseSelectedLog()
	{
		selected = null;
		copiedValue = null;
	}

	private bool IsCopied(string? value)
		=> !string.IsNullOrEmpty(value) && string.Equals(copiedValue, value, StringComparison.Ordinal);

	private async Task CopyValueAsync(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return;

		try
		{
			await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", value);
			copiedValue = value;
			StateHasChanged();
			await Task.Delay(1500);
			if (string.Equals(copiedValue, value, StringComparison.Ordinal))
			{
				copiedValue = null;
				StateHasChanged();
			}
		}
		catch (JSException)
		{
			// Clipboard access can be unavailable in insecure or restricted contexts.
		}
	}

	private static MarkupString FormatMessage(string? message)
	{
		var html = WebUtility.HtmlEncode(message ?? string.Empty);
		html = Regex.Replace(html, @"(https?://[^\s<]+)", "<a href=\"$1\" target=\"_blank\" rel=\"noopener noreferrer\">$1</a>");
		html = Regex.Replace(html, @"'([^']+)'", "'<code>$1</code>'");
		return new MarkupString(html);
	}

	private static MarkupString FormatProperties(string? properties)
	{
		var json = properties ?? string.Empty;
		try
		{
			using var document = JsonDocument.Parse(json);
			json = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
		}
		catch (JsonException)
		{
			// Keep non-JSON property payloads readable in the code block.
		}

		var html = WebUtility.HtmlEncode(json);
		html = Regex.Replace(
			html,
			@"(&quot;[^&]*?&quot;)(\s*:)|(&quot;[^&]*?&quot;)|\b(true|false|null|-?\d+(?:\.\d+)?)\b",
			match =>
			{
				if (match.Groups[2].Success)
					return $"<span class=\"ats-json-key\">{match.Groups[1].Value}</span>{match.Groups[2].Value}";

				if (match.Groups[3].Success)
					return $"<span class=\"ats-json-string\">{match.Groups[3].Value}</span>";

				return $"<span class=\"ats-json-number\">{match.Value}</span>";
			});
		return new MarkupString(html);
	}

	private static string GetLevelClass(string? logLevel)
		=> logLevel?.Trim().ToLowerInvariant() switch
		{
			"warning" => "warning",
			"error" or "fatal" => "error",
			"information" or "info" => "info",
			_ => "info"
		};

	private static string ShortSource(string? source)
		=> string.IsNullOrWhiteSpace(source) ? "—" : source.Split('.').Last();
}
