namespace Auth.Data.Repository;

public interface IRegistrationRepository : ILoginRepository
{
	Task<bool> InsertOtpVerification(OtpVerification otpVerification);
	Task<OtpVerification> IsUserEmailExistInOtpVerificationAsync(string email, bool isUsed);
	Task<OtpVerification> OtpVerificationUserData(OtpVerificationRequestDTO otpVerificationRequestDTO);
	Task<bool> UpdateVerificationCodeAsync(OtpVerification userDto);
	Task<bool> UpdateValidateOtp(OtpVerification otpVerification);
	Task<bool> DeleteOtpRecordIfExpired(OtpVerification otpVerification);
	Task<Authusers> IsUserEmailExistAsync(string email);
	Task<RegisterResponseDTO> RegisterUserAsync(RegisterRequestDTO userDto);
}
