namespace FrontendWebassembly.Services.GeneralHandler;

using System.Net;

public class InterceptorHandler : DelegatingHandler
{
	private readonly HttpClient _httpClient;
	private readonly IRefreshTokenService _refreshTokenService;

	public InterceptorHandler(
		IHttpClientFactory httpClientFactory,
		IRefreshTokenService refreshTokenService)
	{
		_httpClient = httpClientFactory.CreateClient("RefreshAPI");
		this._refreshTokenService = refreshTokenService;
	}

	// The handler is registered transient, so these are static: every request that
	// 401s must queue behind the same refresh, not start its own.
	//
	// Refresh rotation is destructive and single-use - AuthRepository.RotateRefreshTokenAsync
	// overwrites TokenHash in one conditional UPDATE, so of N concurrent refreshes
	// carrying the same cookie exactly one wins and the rest are rejected. Any page
	// doing Task.WhenAll therefore produced N-1 spurious failures once the 10-minute
	// access token expired.
	private static readonly SemaphoreSlim _refreshLock = new(1, 1);

	// Incremented on every successful refresh. A request that 401s reads this before
	// queueing; if it changed while waiting, someone else already refreshed and this
	// request only needs to retry - re-refreshing would invalidate the cookie it is
	// about to use.
	private static long _refreshGeneration;

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		var response = await base.SendAsync(request, cancellationToken);

		if (response.StatusCode != HttpStatusCode.Unauthorized)
			return response;

		var observedGeneration = Interlocked.Read(ref _refreshGeneration);

		await _refreshLock.WaitAsync(cancellationToken);
		try
		{
			// Unchanged generation means this request is the first to arrive with a
			// stale token, so it owns the refresh. Otherwise the wait was the refresh.
			if (Interlocked.Read(ref _refreshGeneration) == observedGeneration)
			{
				var refreshResponse = await _refreshTokenService.GetNewAccessAndRefreshToken();

				if (!string.IsNullOrEmpty(refreshResponse.errorMessage))
				{
					// Deliberately does not log out. A failed refresh is not proof the
					// session is dead, and revoking here would tear down cookies that a
					// concurrent request may have just legitimately refreshed. Surfacing
					// the 401 is enough: MainLayout.OnInitializedAsync already redirects
					// to /login when IsAuthenticated() fails.
					return response;
				}

				Interlocked.Increment(ref _refreshGeneration);
			}
		}
		finally
		{
			_refreshLock.Release();
		}

		// Clone and retry; the refreshed access token is already in the HttpOnly cookie.
		var clonedRequest = await CloneAsync(request);

		return await base.SendAsync(clonedRequest, cancellationToken);
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
