using FrontendWebassembly.DTO.Logging;

namespace FrontendWebassembly.Pages.Administration;

public partial class PlatformLogs
{
	private readonly List<PlatformLogDTO> logs = [];
	private readonly string[] applications = ["ATS", "Auth", "PhilSys", "AIAgent", "CNX", "SSO", "Platform"];
	private readonly string[] levels = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];
	private string? application;
	private string? level;
	private string? search;
	private string? nextCursor;
	private string? error;
	private bool isLoading;
	private PlatformLogDTO? selected;

	protected override Task OnInitializedAsync() => LoadFirstPageAsync();

	private async Task LoadFirstPageAsync()
	{
		logs.Clear(); nextCursor = null;
		await LoadAsync(null);
	}

	private Task LoadMoreAsync() => LoadAsync(nextCursor);

	private async Task LoadAsync(string? cursor)
	{
		if (isLoading) return;
		isLoading = true; error = null;
		try
		{
			var result = await LogService.GetLogsAsync(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow,
				application, level, search, cursor, 50);
			logs.AddRange(result.Items);
			nextCursor = result.NextCursor;
		}
		catch (Exception exception) { error = $"Unable to load platform logs: {exception.Message}"; }
		finally { isLoading = false; }
	}

	private static string ShortSource(string? source)
		=> string.IsNullOrWhiteSpace(source) ? "—" : source.Split('.').Last();
}
