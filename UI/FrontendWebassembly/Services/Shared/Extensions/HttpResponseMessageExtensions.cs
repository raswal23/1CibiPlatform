namespace FrontendWebassembly.Services.Shared.Extensions;

public static class HttpResponseMessageExtensions
{
	public static async Task<string> ReadErrorDetailAsync(
		this HttpResponseMessage response, CancellationToken cancellationToken = default)
	{
		try
		{
			var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);

			if (!string.IsNullOrWhiteSpace(error?.Detail))
			{
				return error!.Detail;
			}

			if (!string.IsNullOrWhiteSpace(error?.Title))
			{
				return error!.Title;
			}
		}
		catch (Exception ex) when (ex is JsonException or NotSupportedException)
		{
			// Body was not a ProblemDetails payload (e.g. HTML or empty) — fall through to the generic message.
		}

		return $"Request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).";
	}
}
