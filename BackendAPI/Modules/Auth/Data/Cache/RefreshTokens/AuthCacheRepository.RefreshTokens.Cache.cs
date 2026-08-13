namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<UserDataDTO> GetNewUserDataAsync(Guid userId)
		{
			return await _authRepository.GetNewUserDataAsync(userId);
		}
	
	public async Task<List<AuthRefreshToken>> IsUserExistAsync(Guid userId)
		{
			return await _authRepository.IsUserExistAsync(userId);
		}
	
	public async Task<AuthRefreshToken> SaveRefreshTokenAsync(Guid userId, string hashToken, DateTime expiryDate)
		{
			return await _authRepository.SaveRefreshTokenAsync(userId, hashToken, expiryDate);
		}

	public Task<AuthRefreshToken?> GetSessionAsync(int sessionId, CancellationToken cancellationToken = default) =>
		_authRepository.GetSessionAsync(sessionId, cancellationToken);

	public Task<bool> RotateRefreshTokenAsync(int sessionId, string currentHash, string replacementHash, DateTime expiryDate, CancellationToken cancellationToken = default) =>
		_authRepository.RotateRefreshTokenAsync(sessionId, currentHash, replacementHash, expiryDate, cancellationToken);
	
	public async Task<bool> UpdateRevokeReasonAsync(AuthRefreshToken authRefreshToken, string reason)
		{
			return await _authRepository.UpdateRevokeReasonAsync(authRefreshToken, reason);
		}
	
	public Task<bool> UpdateRefreshTokenAsync(AuthRefreshToken authRefreshToken)
		{
			return _authRepository.UpdateRefreshTokenAsync(authRefreshToken);
		}
	
	public Task<AuthRefreshToken?> SearchUserRefreshToken(Guid userId, string refreshToken)
		{
			return _authRepository.SearchUserRefreshToken(userId, refreshToken);
		}
	
	public Task<AuthRefreshToken?> FindActiveRefreshTokenByHashAsync(string tokenHash)
		{
			return _authRepository.FindActiveRefreshTokenByHashAsync(tokenHash);
		}
}
