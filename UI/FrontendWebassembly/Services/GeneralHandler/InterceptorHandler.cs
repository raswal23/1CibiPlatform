namespace FrontendWebassembly.Services.GeneralHandler;

using System.Net;
using System.Net.Http.Headers;

public class InterceptorHandler : DelegatingHandler
{
	private readonly HttpClient _httpClient;
	private readonly IRefreshTokenService _refreshTokenService;
	private readonly LocalStorageService _localStorageService;

	public InterceptorHandler(
		IHttpClientFactory httpClientFactory,
		IRefreshTokenService refreshTokenService,
		LocalStorageService localStorageService)
	{
		_httpClient = httpClientFactory.CreateClient("RefreshAPI");
		this._refreshTokenService = refreshTokenService;
		this._localStorageService = localStorageService;
	}

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{

		var response = await base.SendAsync(request, cancellationToken);
		if (response.StatusCode == HttpStatusCode.Unauthorized)
		{
			var userId = await _localStorageService.GetItemAsync<Guid>("UserId");

			if (userId == Guid.Empty)
			{
				return response;
			}

			var refreshResponse = await _refreshTokenService.GetNewAccessAndRefreshToken(userId);

			if (!string.IsNullOrEmpty(refreshResponse.errorMessage))
			{
				await _refreshTokenService.Logout();
				return response;
			}

			// Clone the original request and attach the new access token
			var clonedRequest = await CloneAsync(request);

			clonedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshResponse.token);

			return await base.SendAsync(clonedRequest, cancellationToken);
		}
		;
		return response;
	}

	private async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage req)
	{
		// Create a new request with the same method and URL
		var clone = new HttpRequestMessage(req.Method, req.RequestUri);

		// Copy the content (body)
		if (req.Content != null)
		{
			var contentBytes = await req.Content.ReadAsByteArrayAsync();
			clone.Content = new ByteArrayContent(contentBytes);

			// Copy content headers
			foreach (var h in req.Content.Headers)
				clone.Content.Headers.Add(h.Key, h.Value);
		}

		// Copy request headers
		foreach (var h in req.Headers)
			clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

		return clone;
	}

}
