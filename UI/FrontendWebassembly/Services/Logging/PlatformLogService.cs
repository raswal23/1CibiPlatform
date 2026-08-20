using FrontendWebassembly.DTO.Logging;
using System.Net;

namespace FrontendWebassembly.Services.Logging;

public sealed class PlatformLogService(IHttpClientFactory httpClientFactory) : IPlatformLogService
{
	private readonly HttpClient _httpClient = httpClientFactory.CreateClient("API");

	public async Task<PlatformLogPageDTO> GetLogsAsync(DateTimeOffset? from, DateTimeOffset? to, string? application,
		string? level, string? search, string? cursor, int pageSize, CancellationToken cancellationToken = default)
	{
		var parameters = new List<string> { $"pageSize={Math.Clamp(pageSize, 1, 100)}" };
		Add(parameters, "from", from?.ToString("O"));
		Add(parameters, "to", to?.ToString("O"));
		Add(parameters, "application", application);
		Add(parameters, "level", level);
		Add(parameters, "search", search);
		Add(parameters, "cursor", cursor);
		var response = await _httpClient.GetAsync($"platform-logging/logs?{string.Join('&', parameters)}", cancellationToken);
		response.EnsureSuccessStatusCode();
		return (await response.Content.ReadFromJsonAsync<PlatformLogPageDTO>(cancellationToken: cancellationToken))!;
	}

	public async Task<PlatformLogDTO?> GetLogAsync(long id, CancellationToken cancellationToken = default)
	{
		var response = await _httpClient.GetAsync($"platform-logging/logs/{id}", cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound) return null;
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<PlatformLogDTO>(cancellationToken: cancellationToken);
	}

	private static void Add(ICollection<string> parameters, string name, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value)) parameters.Add($"{name}={Uri.EscapeDataString(value)}");
	}
}
