namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<PasswordResetToken> GetUserTokenAsync(string tokenHash)
		{
			return await _authRepository.GetUserTokenAsync(tokenHash);
		}
	
	public async Task<bool> SaveToResetPasswordToken(PasswordResetToken passwordResetToken)
		{
			return await _authRepository.SaveToResetPasswordToken(passwordResetToken);
		}
	
	public async Task<bool> UpdateAuthUserPassword(Authusers authusers)
		{
			return await _authRepository.UpdateAuthUserPassword(authusers);
		}
	
	public async Task<bool> UpdatePasswordResetTokenAsUsedAsync(PasswordResetToken passwordResetToken)
		{
			return await _authRepository.UpdatePasswordResetTokenAsUsedAsync(passwordResetToken);
		}
}
