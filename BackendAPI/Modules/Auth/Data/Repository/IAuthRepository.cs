namespace Auth.Data.Repository;

public interface IAuthRepository
{
	// Get methods
	Task<PaginatedResult<UsersDTO>> GetUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<UsersDTO>> GetUnapprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<ApplicationsDTO>> GetApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<SubMenusDTO>> GetSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<AppSubRolesDTO>> GetAppSubRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<RolesDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<AuthAttempts>> GetLockedUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<LoginDTO> GetUserDataAsync(LoginWebCred cred);
	Task<UserDataDTO> GetNewUserDataAsync(Guid userId);
	Task<Authusers> GetRawUserAsync(Guid id);
	Task<PasswordResetToken> GetUserTokenAsync(string tokenHash);
	Task<AuthApplication> GetApplicationAsync(int applicationId);
	Task<AuthUserAppRole> GetAppSubRoleAsync(int appSubRoleId);
	Task<AuthSubMenu> GetSubMenuAsync(int applicationId);
	Task<AuthRole> GetRoleAsync(int roleId);
	Task<Authusers> GetUserAsync(string email);
	Task<AuthAttempts> GetLockedUserAsync(Guid userId);

	// Search methods
	Task<PaginatedResult<UsersDTO>> SearchUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<UsersDTO>> SearchUnApprovedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<ApplicationsDTO>> SearchApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<SubMenusDTO>> SearchSubMenusAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<AppSubRolesDTO>> SearchAppSubRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<RolesDTO>> SearchRoleAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<PaginatedResult<AuthAttempts>> SearchLockedUserAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<AuthRefreshToken> SearchUserRefreshToken(Guid userId, string refreshToken);

	// Delete methods
	Task<bool> DeleteLockedUserAsync(AuthAttempts authAttempts);

	// Save methodsZ
	Task<bool> SaveUserAsync(Authusers user);
	Task<bool> SaveRefreshTokenAsync(Guid userId, string hashToken, DateTime expiryDate);
	Task<bool> SaveToResetPasswordToken(PasswordResetToken passwordResetToken);
	Task<bool> SaveLockedUserAsync(AuthAttempts userAttempt);

	// OTP / Verification methods
	Task<bool> InsertOtpVerification(OtpVerification otpVerification);
	Task<OtpVerification> IsUserEmailExistInOtpVerificationAsync(string email, bool isUsed);
	Task<OtpVerification> OtpVerificationUserData(OtpVerificationRequestDTO otpVerificationRequestDTO);
	Task<bool> UpdateVerificationCodeAsync(OtpVerification userDto);
	Task<bool> UpdateValidateOtp(OtpVerification otpVerification);
	Task<bool> DeleteOtpRecordIfExpired(OtpVerification otpVerification);

	// Update methods
	Task<bool> UpdateRevokeReasonAsync(AuthRefreshToken authRefreshToken, string reason);
	Task<bool> UpdateAuthUserPassword(Authusers authusers);
	Task<bool> UpdateRefreshTokenAsync(AuthRefreshToken authRefreshToken);
	Task<bool> UpdatePasswordResetTokenAsUsedAsync(PasswordResetToken passwordResetToken);

	// Existence / lookup methods
	Task<List<AuthRefreshToken>> IsUserExistAsync(Guid userId);
	Task<Authusers> IsUserEmailExistAsync(string email);

	// Registration
	Task<RegisterResponseDTO> RegisterUserAsync(RegisterRequestDTO userDto);

	//User Edit
	Task<Authusers> EditUserAsync(Authusers user);

	// Application CRUD
	Task<bool> AddApplicationAsync(AddApplicationDTO application);
	Task<AuthApplication> EditApplicationAsync(AuthApplication application);
	Task<bool> DeleteApplicationAsync(AuthApplication application);

	// SubMenu CRUD
	Task<bool> AddSubMenuAsync(AddSubMenuDTO subMenu);
	Task<AuthSubMenu> EditSubMenuAsync(AuthSubMenu subMenu);
	Task<bool> DeleteSubMenuAsync(AuthSubMenu subMenu);

	// AppSubRole CRUD
	Task<bool> AddAppSubRoleAsync(AddAppSubRoleDTO appSubRole);
	Task<AuthUserAppRole> EditAppSubRoleAsync(AuthUserAppRole appSubRole);
	Task<bool> DeleteAppSubRoleAsync(AuthUserAppRole appSubRole);

	// Role CRUD
	Task<bool> AddRoleAsync(AddRoleDTO role);
	Task<AuthRole> EditRoleAsync(AuthRole role);
	Task<bool> DeleteRoleAsync(AuthRole role);
}
