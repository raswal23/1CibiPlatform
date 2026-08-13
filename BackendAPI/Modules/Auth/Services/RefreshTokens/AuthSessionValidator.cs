namespace Auth.Services;

public sealed class AuthSessionValidator : IAuthSessionValidator
{
	private const string SessionTag = "auth-sessions";
	private readonly IRefreshTokenRepository _repository;
	private readonly HybridCache _cache;

	public AuthSessionValidator(IRefreshTokenRepository repository, HybridCache cache)
	{
		_repository = repository;
		_cache = cache;
	}

	public async Task<bool> IsActiveAsync(int sessionId, Guid userId, CancellationToken cancellationToken = default)
	{
		var session = await _cache.GetOrCreateAsync(
			$"auth-session:{sessionId}",
			async token => await _repository.GetSessionAsync(sessionId, token),
			new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(30), LocalCacheExpiration = TimeSpan.FromSeconds(30) },
			tags: [SessionTag],
			cancellationToken: cancellationToken);

		return session is not null && session.UserId == userId && session.IsActive && session.ExpiresAt > DateTime.UtcNow;
	}

	public ValueTask InvalidateAsync(int sessionId, CancellationToken cancellationToken = default) =>
		_cache.RemoveAsync($"auth-session:{sessionId}", cancellationToken);
}
