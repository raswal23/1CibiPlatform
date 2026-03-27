namespace Auth.Services;

public class ForgotPasswordService : IForgotPasswordService
{
	private readonly IAuthRepository _authRepository;
	private readonly ILogger<ForgotPasswordService> _logger;
	private readonly IEmailService _emailService;
	private readonly IConfiguration _configuration;
	private readonly ISecureToken _secureToken;
	private readonly IHashService _hashService;
	private readonly IPasswordHasherService _passwordHasherService;
	private readonly string _frontendBaseUrl;
	private readonly int _passwordTokenExpiryMinutes;

	public ForgotPasswordService(
		IAuthRepository authRepository,
		ILogger<ForgotPasswordService> logger,
		IEmailService emailService,
		IConfiguration configuration,
		ISecureToken secureToken,
		IHashService hashService,
		IPasswordHasherService passwordHasherService)
	{
		this._authRepository = authRepository;
		this._logger = logger;
		this._emailService = emailService;
		this._configuration = configuration;
		this._secureToken = secureToken;
		this._hashService = hashService;
		this._passwordHasherService = passwordHasherService;
		this._frontendBaseUrl = _configuration.GetValue<string>("FrontEndMetadata:ForgotPasswordUrl") ?? "";
		this._passwordTokenExpiryMinutes = _configuration.GetValue<int>("Email:PasswordTokenExpirationInMinutes");
	}

	public async Task<bool> ForgotPasswordAsync(string email)
	{

		var logContext = new
		{
			Action = "LoggingOutUser",
			Step = "StartLoggingOut",
			Email = email,
			Timestamp = DateTime.UtcNow
		};


		_logger.LogInformation("ForgotPassword called with email: {@Context}", logContext);

		var user = await _authRepository.IsUserEmailExistAsync(email);

		if (user == null)
		{
			_logger.LogWarning("No user found with email: {@Context}", logContext);
			throw new NotFoundException("Email not found.");
		}

		var secureToken = _secureToken.GenerateSecureToken();

		if (secureToken == null)
		{
			_logger.LogError("Failed to generate secure token for email: {@Context}", logContext);
			throw new Exception("Failed to generate secure token.");
		}

		var hashedToken = _hashService.Hash(secureToken);

		var resetLink = $"{_frontendBaseUrl}/reset-password?token={System.Net.WebUtility.UrlEncode(hashedToken)}";

		var name = $"{user.FirstName} {user.LastName}";

		var emailBody = _emailService.SendPasswordResetBody(name, resetLink, this._passwordTokenExpiryMinutes);

		var isSent = await _emailService.SendEmailAsync(
			toEmail: email,
			subject: "Password Reset Request",
			body: emailBody
		);

		if (!isSent)
		{
			_logger.LogError("Failed to send password reset email to: {@Context}", logContext);
			throw new Exception("Failed to send password reset email.");
		}

		var userDetailsPasswordToken = new PasswordResetToken
		{
			UserId = user.Id,
			TokenHash = hashedToken,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddMinutes(_passwordTokenExpiryMinutes),
			IsUsed = false
		};

		var saveTokenResult = await _authRepository.SaveToResetPasswordToken(userDetailsPasswordToken);

		if (!saveTokenResult)
		{
			_logger.LogError("Failed to save password reset token for email: {@Context}", logContext);
			throw new Exception("Failed to save password reset token.");
		}

		_logger.LogInformation("Password reset token generated and email sent for user: {@Context}", logContext);

		return true;
	}

	public async Task<bool> ResetPasswordAsync(
		string tokenHash,
		string newPassword)
	{
		var isTokenValid = await this.IsTokenValidInternal(tokenHash);

		var logContext = new
		{
			Action = "ResetPassword",
			Step = "StartResetting",
			TokenContext = tokenHash,
			Timestamp = DateTime.UtcNow
		};

		if (isTokenValid.userId == Guid.Empty)
		{
			_logger.LogWarning("Invalid or expired token for user: {@Context}", logContext);
			throw new UnauthorizedAccessException("Invalid or expired token.");
		}

		_logger.LogInformation("ResetPasswordAsync called for user: {@Context}", logContext);

		var userNewPassword = await _authRepository.GetRawUserAsync(isTokenValid.userId);

		if (userNewPassword == null)
		{
			_logger.LogWarning("No user found with ID: {@Context}", logContext);
			throw new NotFoundException("User not found.");
		}

		var newHashedPassword = _passwordHasherService.HashPassword(newPassword);

		userNewPassword.PasswordHash = newHashedPassword;

		var isUpdated = await _authRepository.UpdateAuthUserPassword(userNewPassword);

		if (!isUpdated)
		{
			_logger.LogError("Failed to update password for user ID: {@Context}", logContext);
			throw new Exception("Failed to update password.");
		}


		var token = await _authRepository.GetUserTokenAsync(tokenHash);

		token.IsUsed = true;
		token.UsedAt = DateTime.UtcNow;

		var isTokenUpdated = await _authRepository.UpdatePasswordResetTokenAsUsedAsync(token);


		if (!isTokenUpdated)
		{
			_logger.LogError("Failed to update password reset token for user ID: {@Context}", logContext);
			throw new Exception("Failed to update password reset token.");
		}

		_logger.LogInformation("Password updated successfully for user ID: {@Context}", logContext);

		return true;
	}

	private async Task<PasswordResetTokenDTO> IsTokenValidInternal(string tokenHash)
	{
		var token = await _authRepository.GetUserTokenAsync(tokenHash);
		if (token == null || token.IsUsed || token.ExpiresAt < DateTime.UtcNow)
		{
			return new PasswordResetTokenDTO(Guid.Empty);
		}
		return new PasswordResetTokenDTO(token.UserId);
	}

	public async Task<bool> IsTokenValid(string tokenHash)
	{
		var logContext = new
		{
			Action = "CheckingTokenIfValid",
			Step = "StartChecking",
			TokenContext = tokenHash,
			Timestamp = DateTime.UtcNow
		};

		var token = await _authRepository.GetUserTokenAsync(tokenHash);
		if (token == null || token.IsUsed || token.ExpiresAt < DateTime.UtcNow)
		{
			_logger.LogWarning("Invalid or expired token: {@Context}", logContext);
			return false;
		}

		_logger.LogInformation("Token is valid: {@Context}", logContext);

		return true;
	}
}
