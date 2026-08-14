namespace Auth.Data.Repository;

public interface IRefreshTokenRepository
{
	Task<UserDataDTO> GetNewUserDataAsync(Guid userId);
	Task<AuthRefreshToken?> SearchUserRefreshToken(Guid userId, string refreshToken);
	Task<AuthRefreshToken?> FindActiveRefreshTokenByHashAsync(string tokenHash);
	Task<AuthRefreshToken> SaveRefreshTokenAsync(Guid userId, string hashToken, DateTime expiryDate);
	Task<AuthRefreshToken?> GetSessionAsync(int sessionId, CancellationToken cancellationToken = default);
	Task<bool> RotateRefreshTokenAsync(int sessionId, string currentHash, string replacementHash, DateTime expiryDate, CancellationToken cancellationToken = default);
	Task<bool> UpdateRevokeReasonAsync(AuthRefreshToken authRefreshToken, string reason);
	Task<bool> UpdateRefreshTokenAsync(AuthRefreshToken authRefreshToken);
	Task<List<AuthRefreshToken>> IsUserExistAsync(Guid userId);
}
