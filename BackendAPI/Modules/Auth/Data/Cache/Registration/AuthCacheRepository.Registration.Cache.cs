namespace Auth.Data.Cache;

public partial class AuthCacheRepository
{
	public async Task<bool> DeleteOtpRecordIfExpired(OtpVerification otpVerification)
		{
			return await _authRepository.DeleteOtpRecordIfExpired(otpVerification);
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
	
	public async Task<OtpVerification> OtpVerificationUserData(OtpVerificationRequestDTO otpVerificationRequestDTO)
		{
			return await _authRepository.OtpVerificationUserData(otpVerificationRequestDTO);
		}
	
	public async Task<RegisterResponseDTO> RegisterUserAsync(RegisterRequestDTO userDto)
		{
			return await _authRepository.RegisterUserAsync(userDto);
		}
	
	public async Task<bool> UpdateValidateOtp(OtpVerification otpVerification)
		{
			return await _authRepository.UpdateValidateOtp(otpVerification);
		}
	
	public async Task<bool> UpdateVerificationCodeAsync(OtpVerification userDto)
		{
			return await _authRepository.UpdateVerificationCodeAsync(userDto);
		}
}
