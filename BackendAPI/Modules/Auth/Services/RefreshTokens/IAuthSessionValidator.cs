namespace Auth.Services;

public interface IAuthSessionValidator
{
	Task<bool> IsActiveAsync(int sessionId, Guid userId, CancellationToken cancellationToken = default);
	ValueTask InvalidateAsync(int sessionId, CancellationToken cancellationToken = default);
}
