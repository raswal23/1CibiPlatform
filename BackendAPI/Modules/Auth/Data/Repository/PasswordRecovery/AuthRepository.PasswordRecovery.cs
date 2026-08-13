namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<PasswordResetToken> GetUserTokenAsync(string tokenHash)
		{
			var passwordResetToken = await _dbcontext.PasswordResetToken
				.Where(prt => prt.TokenHash == tokenHash &&
							  prt.IsUsed == false)
				.FirstOrDefaultAsync();
	
			return passwordResetToken!;
		}
	
	public async Task<bool> SaveToResetPasswordToken(PasswordResetToken passwordResetToken)
		{
	
			await _dbcontext.PasswordResetToken.AddAsync(passwordResetToken);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
		}
	
	public async Task<bool> UpdateAuthUserPassword(Authusers authusers)
		{
			_dbcontext.AuthUsers.Update(authusers);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
		}
	
	public async Task<bool> UpdatePasswordResetTokenAsUsedAsync(PasswordResetToken passwordResetToken)
		{
			_dbcontext.PasswordResetToken.Update(passwordResetToken);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
		}
}
