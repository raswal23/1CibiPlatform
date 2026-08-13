namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<UserDataDTO> GetNewUserDataAsync(Guid userId)
		{
			var userData = await (from user in _dbcontext.AuthUsers
								  join authRefreshToken in _dbcontext.AuthRefreshToken
															 on user.Id equals authRefreshToken.UserId
								  join userRole in _dbcontext.AuthUserAppRoles
															 on user.Id equals userRole.UserId into userRolesGroup
								  where authRefreshToken.UserId == userId &&
								  user.IsActive == true && authRefreshToken.IsActive == true
								  select new UserDataDTO(
								   user.Id,
								   user.PasswordHash,
								   user.Email!,
								   user.FirstName!,
								   user.LastName!,
								   user.MiddleName,
								   authRefreshToken.TokenHash,
								   userRolesGroup.Select(r => r.AppId).Distinct().ToList(),
								   userRolesGroup.GroupBy(r => r.AppId)
												 .Select(g => g.Select(r => r.Submenu).ToList())
												 .ToList(),
								   userRolesGroup.Select(r => r.RoleId).Distinct().ToList()
								  ))
								 .AsNoTracking()
								 .FirstOrDefaultAsync();
	
			return userData!;
		}
	
	public async Task<bool> SaveRefreshTokenAsync(
			Guid userId,
			string hashToken,
			DateTime expiryDate)
		{
			await _dbcontext.AuthRefreshToken.AddAsync(new AuthRefreshToken
			{
				UserId = userId,
				TokenHash = hashToken,
				ExpiresAt = expiryDate
			});
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
		}
	
	public async Task<bool> UpdateRevokeReasonAsync(
			AuthRefreshToken authRefresh,
			string reason)
		{
	
			authRefresh!.RevokedReason = reason;
			authRefresh.IsActive = false;
			authRefresh.RevokedAt = DateTime.UtcNow;
	
			_dbcontext.AuthRefreshToken.Update(authRefresh);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
		}
	
	public async Task<bool> UpdateRefreshTokenAsync(AuthRefreshToken authRefreshToken)
		{
			_dbcontext.AuthRefreshToken.Update(authRefreshToken);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
		}
	
	public async Task<List<AuthRefreshToken>> IsUserExistAsync(Guid userId)
		{
			return await
				_dbcontext.AuthRefreshToken
				.Where(art => art.UserId == userId && art.IsActive)
				.ToListAsync();
		}
	
	public Task<AuthRefreshToken> SearchUserRefreshToken(
			Guid userId,
			string refreshToken)
		{
			var userRefreshTokenData = _dbcontext.AuthRefreshToken
				.Where(art => art.UserId == userId &&
							  art.TokenHash == refreshToken &&
							  art.IsActive == true)
				.FirstOrDefaultAsync();
	
			return userRefreshTokenData;
		}
	
	public async Task<AuthRefreshToken> FindActiveRefreshTokenByHashAsync(string tokenHash)
		{
			return (await _dbcontext.AuthRefreshToken
				.FirstOrDefaultAsync(token =>
					token.TokenHash == tokenHash &&
					token.IsActive &&
					token.ExpiresAt > DateTime.UtcNow))!;
		}
}
