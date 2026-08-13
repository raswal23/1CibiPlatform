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
	
	public async Task<AuthRefreshToken> SaveRefreshTokenAsync(
			Guid userId,
			string hashToken,
			DateTime expiryDate)
		{
			var session = new AuthRefreshToken
			{
				UserId = userId,
				TokenHash = hashToken,
				ExpiresAt = expiryDate
			};
			await _dbcontext.AuthRefreshToken.AddAsync(session);
	
			await _dbcontext.SaveChangesAsync();
	
			return session;
		}

	public Task<AuthRefreshToken?> GetSessionAsync(int sessionId, CancellationToken cancellationToken = default) =>
		_dbcontext.AuthRefreshToken.AsNoTracking().FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);

	public async Task<bool> RotateRefreshTokenAsync(int sessionId, string currentHash, string replacementHash, DateTime expiryDate, CancellationToken cancellationToken = default)
	{
		var updated = await _dbcontext.AuthRefreshToken
			.Where(session => session.Id == sessionId && session.TokenHash == currentHash && session.IsActive && session.ExpiresAt > DateTime.UtcNow)
			.ExecuteUpdateAsync(update => update
				.SetProperty(session => session.TokenHash, replacementHash)
				.SetProperty(session => session.CreatedAt, DateTime.UtcNow)
				.SetProperty(session => session.ExpiresAt, expiryDate), cancellationToken);
		return updated == 1;
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
	
	public Task<AuthRefreshToken?> SearchUserRefreshToken(
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
	
	public Task<AuthRefreshToken?> FindActiveRefreshTokenByHashAsync(string tokenHash)
		{
			return _dbcontext.AuthRefreshToken
				.FirstOrDefaultAsync(token =>
					token.TokenHash == tokenHash &&
					token.IsActive &&
					token.ExpiresAt > DateTime.UtcNow);
		}
}
