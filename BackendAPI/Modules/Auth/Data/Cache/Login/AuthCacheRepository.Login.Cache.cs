namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<LoginDTO> GetUserDataAsync(LoginWebCred cred)
		{
			return await _authRepository.GetUserDataAsync(cred);
		}
	
	public async Task<bool> SaveUserAsync(Authusers user)
		{
			var result = await _authRepository.SaveUserAsync(user);
	
			if (result != false)
				await _hybridCache.RemoveByTagAsync(UnApprovedUsersTag);
	
			return result!;
		}
}
