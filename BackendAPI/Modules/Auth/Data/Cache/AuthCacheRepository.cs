namespace Auth.Data.Cache;

public class AuthCacheRepository : IAuthRepository
{
	private readonly IAuthRepository _authRepository;
	private readonly HybridCache _hybridCache;

	private const string UsersTag = "users";
	private const string SubMenusTag = "submenus";
	private const string ApplicationsTag = "applications";
	private const string AppSubRolesTag = "appsubroles";
	private const string RolesTag = "roles";
	private const string UnApprovedUsersTag = "unapprovedusers";
	private const string LockedUsersTag = "lockedusers";
	private readonly string UserLockoutDate = "userlockoutdate";
	private readonly string _userAttemptTag = "user_attempt";

	public AuthCacheRepository(
		IAuthRepository authRepository,
		HybridCache hybridCache)
	{
		_authRepository = authRepository;
		_hybridCache = hybridCache;
	}

	public async Task<PaginatedResult<UsersDTO>> GetUserAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var cacheKey = $"users_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UsersDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.GetUserAsync(req, token),
			null,
			tags: [UsersTag],
			cancellationToken);
	}

	public async Task<PaginatedResult<SubMenusDTO>> GetSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"submenus_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<SubMenusDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.GetSubMenusAsync(req, token),
			tags: [SubMenusTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<SubMenusDTO>> SearchSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"submenus_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<SubMenusDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.SearchSubMenusAsync(req, token),
			tags: [SubMenusTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<UsersDTO>> SearchUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"users_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UsersDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.SearchUserAsync(req, token),
			null,
			null,
			cancellationToken);
	}

	public async Task<PaginatedResult<UsersDTO>> GetUnapprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"unapprovedusers_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UsersDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.GetUnapprovedUserAsync(req, token),
			null,
			tags: [UnApprovedUsersTag],
			cancellationToken);
	}

	public async Task<PaginatedResult<UsersDTO>> SearchUnApprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"unapprovedusers_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<UsersDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.SearchUnApprovedUserAsync(req, token),
			null,
			null,
			cancellationToken);
	}

	public async Task<PaginatedResult<ApplicationsDTO>> GetApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"applications_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ApplicationsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.GetApplicationsAsync(req, token),
			tags: [ApplicationsTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<AuthAttempts>> GetLockedUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"lockedusers_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<AuthAttempts>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.GetLockedUsersAsync(req, token),
			tags: [LockedUsersTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<ApplicationsDTO>> SearchApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"applications_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ApplicationsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.SearchApplicationsAsync(req, token),
			tags: [ApplicationsTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<AuthAttempts>> SearchLockedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"lockedusers_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<AuthAttempts>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.SearchLockedUserAsync(req, token),
			tags: [LockedUsersTag],
			cancellationToken: cancellationToken);
	}

	public async Task<bool> DeleteOtpRecordIfExpired(OtpVerification otpVerification)
	{
		return await _authRepository.DeleteOtpRecordIfExpired(otpVerification);
	}

	public async Task<UserDataDTO> GetNewUserDataAsync(Guid userId)
	{
		return await _authRepository.GetNewUserDataAsync(userId);
	}

	public async Task<Authusers> GetRawUserAsync(Guid id)
	{
		return await _authRepository.GetRawUserAsync(id);
	}

	public async Task<LoginDTO> GetUserDataAsync(LoginWebCred cred)
	{
		return await _authRepository.GetUserDataAsync(cred);
	}

	public async Task<PasswordResetToken> GetUserTokenAsync(string tokenHash)
	{
		return await _authRepository.GetUserTokenAsync(tokenHash);
	}

	public async Task<bool> InsertOtpVerification(OtpVerification otpVerification)
	{
		return await _authRepository.InsertOtpVerification(otpVerification);
	}

	public async Task<Authusers> IsUserEmailExistAsync(string email)
	{
		return await _authRepository.IsUserEmailExistAsync(email);
	}

	public async Task<OtpVerification> IsUserEmailExistInOtpVerificationAsync(string email, bool isUsed)
	{
		return await _authRepository.IsUserEmailExistInOtpVerificationAsync(email, isUsed);
	}

	public async Task<List<AuthRefreshToken>> IsUserExistAsync(Guid userId)
	{
		return await _authRepository.IsUserExistAsync(userId);
	}

	public async Task<OtpVerification> OtpVerificationUserData(OtpVerificationRequestDTO otpVerificationRequestDTO)
	{
		return await _authRepository.OtpVerificationUserData(otpVerificationRequestDTO);
	}

	public async Task<RegisterResponseDTO> RegisterUserAsync(RegisterRequestDTO userDto)
	{
		return await _authRepository.RegisterUserAsync(userDto);
	}

	public async Task<bool> SaveRefreshTokenAsync(Guid userId, string hashToken, DateTime expiryDate)
	{
		return await _authRepository.SaveRefreshTokenAsync(userId, hashToken, expiryDate);
	}

	public async Task<bool> SaveToResetPasswordToken(PasswordResetToken passwordResetToken)
	{
		return await _authRepository.SaveToResetPasswordToken(passwordResetToken);
	}

	public async Task<bool> SaveUserAsync(Authusers user)
	{
		var result = await _authRepository.SaveUserAsync(user);

		if (result != false)
			await _hybridCache.RemoveByTagAsync(UnApprovedUsersTag);

		return result!;
	}

	public async Task<bool> UpdateAuthUserPassword(Authusers authusers)
	{
		return await _authRepository.UpdateAuthUserPassword(authusers);
	}

	public async Task<bool> UpdatePasswordResetTokenAsUsedAsync(PasswordResetToken passwordResetToken)
	{
		return await _authRepository.UpdatePasswordResetTokenAsUsedAsync(passwordResetToken);
	}

	public async Task<bool> UpdateRevokeReasonAsync(AuthRefreshToken authRefreshToken, string reason)
	{
		return await _authRepository.UpdateRevokeReasonAsync(authRefreshToken, reason);
	}

	public async Task<bool> UpdateValidateOtp(OtpVerification otpVerification)
	{
		return await _authRepository.UpdateValidateOtp(otpVerification);
	}

	public async Task<bool> UpdateVerificationCodeAsync(OtpVerification userDto)
	{
		return await _authRepository.UpdateVerificationCodeAsync(userDto);
	}

	public async Task<AuthApplication> GetApplicationAsync(int applicationId)
	{
		return await _authRepository.GetApplicationAsync(applicationId);
	}

	public async Task<bool> DeleteApplicationAsync(AuthApplication application)
	{
		var result = await _authRepository.DeleteApplicationAsync(application);

		if (result)
			await _hybridCache.RemoveByTagAsync(ApplicationsTag);

		return result;
	}

	public async Task<bool> AddApplicationAsync(AddApplicationDTO application)
	{
		var result = await _authRepository.AddApplicationAsync(application);

		if (result)
			await _hybridCache.RemoveByTagAsync(ApplicationsTag);

		return result;
	}

	public async Task<AuthApplication> EditApplicationAsync(AuthApplication application)
	{
		var updated = await _authRepository.EditApplicationAsync(application);

		if (updated != null)
			await _hybridCache.RemoveByTagAsync(ApplicationsTag);

		return updated!;
	}

	public async Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu)
	{
		var result = await _authRepository.AddSubMenuAsync(subMenu);

		if (result)
			await _hybridCache.RemoveByTagAsync(SubMenusTag);

		return result;
	}

	public async Task<bool> DeleteSubMenuAsync(AuthSubMenu subMenu)
	{
		var result = await _authRepository.DeleteSubMenuAsync(subMenu);

		if (result)
			await _hybridCache.RemoveByTagAsync(SubMenusTag);

		return result;
	}

	public async Task<AuthSubMenu> EditSubMenuAsync(AuthSubMenu subMenu)
	{
		var updated = await _authRepository.EditSubMenuAsync(subMenu);

		if (updated != null)
			await _hybridCache.RemoveByTagAsync(SubMenusTag);

		return updated!;
	}

	public async Task<AuthSubMenu> GetSubMenuAsync(int applicationId)
	{
		return await _authRepository.GetSubMenuAsync(applicationId);
	}

	public async Task<PaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"authsubrole_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<AppSubRolesDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.GetAppSubRolesAsync(req, token),
			tags: [AppSubRolesTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<AppSubRolesDTO>> SearchAppSubRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"applications_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<AppSubRolesDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.SearchAppSubRoleAsync(req, token),
			tags: [AppSubRolesTag],
			cancellationToken: cancellationToken);
	}

	public async Task<AuthUserAppRole> GetAppSubRoleAsync(int appSubRoleId)
	{
		return await _authRepository.GetAppSubRoleAsync(appSubRoleId);
	}

	public async Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole)
	{
		var result = await _authRepository.AddAppSubRoleAsync(appSubRole);

		if (result)
			await _hybridCache.RemoveByTagAsync(AppSubRolesTag);

		return result;
	}

	public async Task<bool> DeleteAppSubRoleAsync(AuthUserAppRole appSubRole)
	{
		var result = await _authRepository.DeleteAppSubRoleAsync(appSubRole);

		if (result)
			await _hybridCache.RemoveByTagAsync(AppSubRolesTag);

		return result;
	}

	public async Task<AuthUserAppRole> EditAppSubRoleAsync(AuthUserAppRole appSubRole)
	{
		var updated = await _authRepository.EditAppSubRoleAsync(appSubRole);

		if (updated != null)
			await _hybridCache.RemoveByTagAsync(AppSubRolesTag);

		return updated!;
	}

	public async Task<PaginatedResult<RolesDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"roles_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<RolesDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.GetRolesAsync(req, token),
			tags: [RolesTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<RolesDTO>> SearchRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"roles_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<RolesDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _authRepository.SearchRoleAsync(req, token),
			tags: [RolesTag],
			cancellationToken: cancellationToken);
	}

	public async Task<bool> AddRoleAsync(AddRoleDTO role)
	{
		var result = await _authRepository.AddRoleAsync(role);

		if (result)
			await _hybridCache.RemoveByTagAsync(RolesTag);

		return result;
	}

	public async Task<bool> DeleteRoleAsync(AuthRole role)
	{
		var result = await _authRepository.DeleteRoleAsync(role);

		if (result)
			await _hybridCache.RemoveByTagAsync(RolesTag);

		return result;
	}

	public async Task<AuthRole> GetRoleAsync(int roleId)
	{
		return await _authRepository.GetRoleAsync(roleId);
	}

	public async Task<AuthRole> EditRoleAsync(AuthRole role)
	{
		var updated = await _authRepository.EditRoleAsync(role);

		if (updated != null)
			await _hybridCache.RemoveByTagAsync(RolesTag);

		return updated!;
	}

	public async Task<Authusers> EditUserAsync(Authusers user)
	{
		var updated = await _authRepository.EditUserAsync(user);

		if (updated != null)
			await _hybridCache.RemoveByTagAsync(UsersTag);
		await _hybridCache.RemoveByTagAsync(UnApprovedUsersTag);

		return updated!;
	}

	public Task<bool> UpdateRefreshTokenAsync(AuthRefreshToken authRefreshToken)
	{
		return _authRepository.UpdateRefreshTokenAsync(authRefreshToken);
	}

	public Task<AuthRefreshToken> SearchUserRefreshToken(Guid userId, string refreshToken)
	{
		return _authRepository.SearchUserRefreshToken(userId, refreshToken);
	}

	public async Task<Authusers> GetUserAsync(string email)
	{
		return await _authRepository.GetUserAsync(email);
	}

	public async Task<AuthAttempts> GetLockedUserAsync(Guid userId)
	{
		var cacheKey = $"{UserLockoutDate}_{userId}";

		return await _hybridCache.GetOrCreateAsync<AuthAttempts>(
			cacheKey,
			async (token) => await _authRepository.GetLockedUserAsync(userId),
			tags: [UserLockoutDate]);
	}

	public async Task<bool> DeleteLockedUserAsync(AuthAttempts lockedUser)
	{
		var cachekeyForDate = $"{UserLockoutDate}_{lockedUser.UserId}";
		var cacheKeyForAttempt = $"{_userAttemptTag}_{lockedUser.UserId}";

		var result = await _authRepository.DeleteLockedUserAsync(lockedUser);

		if (result)
		{
			await _hybridCache.RemoveAsync(cacheKeyForAttempt);
			await _hybridCache.RemoveByTagAsync(LockedUsersTag);
			await _hybridCache.RemoveAsync(cachekeyForDate);
		}

		return result;
	}

	public async Task<bool> SaveLockedUserAsync(AuthAttempts userAttempt)
	{
		var result = await _authRepository.SaveLockedUserAsync(userAttempt);
		var cachekeyForDate = $"{UserLockoutDate}_{userAttempt.UserId}";

		if (result)
		{
			await _hybridCache.RemoveByTagAsync(LockedUsersTag);
			await _hybridCache.RemoveAsync(cachekeyForDate);
		}

		return result;
	}
}
