namespace Auth.Services;

public interface IForgotPasswordService
{
	Task<bool> ForgotPasswordAsync(string email);

	Task<bool> ResetPasswordAsync(string hashToken, string newPassword);

	Task<bool> IsTokenValid(string tokenHash);

}
