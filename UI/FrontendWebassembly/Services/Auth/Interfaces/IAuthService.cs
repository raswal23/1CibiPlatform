namespace FrontendWebassembly.Services.Auth.Interfaces;

public interface IAuthService
{
	Task<AuthResponseDTO> Login(LoginCred cred);

	Task<bool> IsAuthenticated();

	Task<RegisterResponseDTO> Register(RegisterRequestDTO registerRequestDTO);

	Task<OtpSessionResponseDTO> IsOtpSessionValid(OtpSessionRequestDTO otpRequestDTO);

	Task<OtpSessionResponseDTO> OtpVerification(OtpVerificationRequestDTO otpVerificationRequestDTO);

	Task<OTPResendResponseDTO> OtpResendAsync(OTPResendRequestDTO otpResendRequestDTO);

	Task<SendEmailForgotPasswordResponseDTO> ForgotPasswordSendEmail(SendEmailForgotPasswordRequestDTO sendEmailForgotPasswordRequestDTO);

	Task<IsChangePasswordTokenValidResponseDTO> IsForgotPasswordTokenValid(ForgotPasswordTokenRequestDTO forgotPasswordTokenRequestDTO);

	Task<UpdatePasswordResponseDTO> UpdatePassword(UpdatePasswordRequestDTO updatePasswordRequestDTO);

	Task<bool> Logout();

}
