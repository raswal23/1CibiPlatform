namespace Auth.Services;

public class RegisterService : IRegisterService
{
	private readonly IEmailService _emailService;
	private readonly IPasswordHasherService _passwordHasherService;
	private readonly ILogger<RegisterService> _logger;
	private readonly IAuthRepository _authRepository;
	private readonly IHashService _hashService;
	private readonly IOtpService _otpService;
	private readonly IConfiguration _configuration;
	private readonly int _otpExpiryMinutes;

	public RegisterService(
		IEmailService emailService,
		IPasswordHasherService passwordHasherService,
		IAuthRepository authRepository,
		IHashService hashService,
		IOtpService otpService,
		IConfiguration configuration,
		ILogger<RegisterService> logger)
	{
		this._emailService = emailService;
		this._passwordHasherService = passwordHasherService;
		this._logger = logger;
		this._authRepository = authRepository;
		this._hashService = hashService;
		this._otpService = otpService;
		this._configuration = configuration;

		this._otpExpiryMinutes = _configuration.GetValue<int>("Email:OtpExpirationInMinutes");

	}

	public async Task<OtpVerificationResponse> RegisterAsync(RegisterRequestDTO registerRequestDTO)
	{

		_logger.LogInformation("Starting registration process for email: {Context}", registerRequestDTO.Email);

		var logContext = new
		{
			Action = "RegisterUser",
			Step = "StartRegistration",
			Email = registerRequestDTO.Email,
			Timestamp = DateTime.UtcNow
		};

		var isUserEmailExist = await _authRepository.IsUserEmailExistInOtpVerificationAsync(registerRequestDTO.Email, true);

		if (isUserEmailExist != null)
		{
			_logger.LogWarning("Email already in use: {@Context}", logContext);
			throw new Exception("Email already in use.");
		}

		var otp = _otpService.GenerateOtp();


		if (otp == null)
		{
			_logger.LogError("Failed to generate OTP for email: {Context}", logContext);
			throw new Exception("Failed to generate OTP.");
		}


		var HashOTP = _hashService.Hash(otp);

		if (HashOTP == null)
		{
			_logger.LogError("Failed to hash OTP for email: {Context}", logContext);
			throw new Exception("Failed to hash OTP.");
		}

		var name = $"{registerRequestDTO.FirstName} {registerRequestDTO.LastName}";

		var otpBody = _emailService.SendOtpBody(name, otp);

		var isSent = await _emailService.SendEmailAsync(
			toEmail: registerRequestDTO.Email,
			subject: "Your OTP Code",
			body: otpBody
		);

		if (!isSent)
		{
			_logger.LogError("Failed to send OTP email to: {@Context}", logContext);
			throw new Exception("Failed to send OTP email.");
		}

		var user = new OtpVerification
		{
			Email = registerRequestDTO.Email,
			OtpId = Guid.CreateVersion7(),
			FirstName = registerRequestDTO.FirstName,
			LastName = registerRequestDTO.LastName,
			MiddleName = registerRequestDTO.MiddleName!,
			PasswordHash = _passwordHasherService.HashPassword(registerRequestDTO.PasswordHash),
			OtpCodeHash = HashOTP,
			IsVerified = false,
			IsUsed = false,
			AttemptCount = 0,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryMinutes)
		};

		var otpUser = await _authRepository.InsertOtpVerification(user);

		if (!otpUser)
		{
			_logger.LogError("Failed to store OTP verification record for email: {Email}", registerRequestDTO.Email);
			throw new Exception("Failed to store OTP verification record.");
		}

		var userOtpResponse = user.Adapt<OtpVerificationResponse>();

		var successContext = new
		{
			Action = "RegisterUser",
			Step = "Completed",
			Email = registerRequestDTO.Email,
			UserId = user.Id,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("User registration completed {@Context}", successContext);

		return userOtpResponse;
	}


	public async Task<bool> VerifyOtpAsync(string email, string otp)
	{
		_logger.LogInformation("Starting OTP verification for email: {Email}", email);

		var logContext = new
		{
			Action = "VerifyingOTPToUser",
			Step = "StartVerification",
			Email = email,
			Timestamp = DateTime.UtcNow
		};

		var existingOtpRecord = await _authRepository.IsUserEmailExistInOtpVerificationAsync(email, false);

		if (existingOtpRecord == null)
		{
			_logger.LogWarning("No OTP record found for email: {@Context}", logContext);
			throw new Exception("No OTP record found for this email.");
		}

		var hashOtp = _hashService.Hash(otp);

		var isOtpValid = _hashService.Verify(hashOtp, existingOtpRecord.OtpCodeHash);

		if (!isOtpValid)
		{
			_logger.LogWarning("Invalid OTP provided for email: {@Context}", logContext);
			existingOtpRecord.AttemptCount += 1;
			await _authRepository.UpdateVerificationCodeAsync(existingOtpRecord);
			throw new Exception("Invalid OTP.");
		}

		if (existingOtpRecord.IsUsed)
		{
			_logger.LogWarning("OTP already used for email: {@Context}", logContext);
			throw new Exception("OTP already used.");
		}

		if (DateTime.UtcNow > existingOtpRecord.ExpiresAt)
		{
			_logger.LogWarning("OTP expired for email: {@Context}", logContext);
			await this.ResendOtpAsync(existingOtpRecord);
			throw new InvalidOperationException("Your OTP has expired. A new code has been sent to your email.");
		}

		existingOtpRecord.IsVerified = true;
		existingOtpRecord.IsUsed = true;
		existingOtpRecord.VerifiedAt = DateTime.UtcNow;
		existingOtpRecord.AttemptCount += 1;

		var isUpdated = await _authRepository.UpdateVerificationCodeAsync(existingOtpRecord);

		if (!isUpdated)
		{
			_logger.LogError("Failed to update OTP record for email: {Email}", email);
			throw new Exception("Failed to update OTP record.");
		}

		var user = new Authusers
		{
			Id = Guid.CreateVersion7(),
			Email = existingOtpRecord.Email,
			PasswordHash = existingOtpRecord.PasswordHash,
			FirstName = existingOtpRecord.FirstName,
			LastName = existingOtpRecord.LastName,
			MiddleName = existingOtpRecord.MiddleName,
		};

		var isSuccess = await _authRepository.SaveUserAsync(user);

		if (!isSuccess)
		{
			_logger.LogError("Failed to save user record for email: {@Context}", logContext);
			throw new Exception("Failed to save user record.");
		}

		_logger.LogInformation("Successfully verified OTP for email: {@Context}", logContext);

		return true;
	}

	public async Task<bool> ManualResendOtpCodeAsync(Guid userId, string email)
	{
		var logContext = new
		{
			Action = "ResendingAutoOTPToUser",
			Step = "StartResending",
			Email = email,
			UserId = userId,
			Timestamp = DateTime.UtcNow
		};

		var otpVerification = await _authRepository.IsUserEmailExistInOtpVerificationAsync(email, false);

		if (otpVerification == null)
		{
			_logger.LogWarning("No OTP record found for email: {@Context}", logContext);
			throw new Exception("No OTP record found for this email.");
		}

		var user = new OtpVerificationRequestDTO(otpVerification.OtpId, otpVerification.Email);

		var isOtpValid = await _authRepository.OtpVerificationUserData(user);

		if (isOtpValid == null)
		{
			_logger.LogWarning("No OTP record found for email: {@Context}", logContext);
			throw new Exception("No OTP record found for this email.");
		}

		var userDetail = await _authRepository.IsUserEmailExistInOtpVerificationAsync(otpVerification.Email, false);

		if (userDetail == null)
		{
			_logger.LogWarning("No OTP record found for email: {@Context}", logContext);
			throw new Exception("No OTP record found for this email.");
		}

		var isSent = await ResendOtpAsync(otpVerification);

		if (!isSent)
		{
			_logger.LogError("Failed to resend OTP email to: {@Context}", logContext);
			throw new Exception("Failed to resend OTP email.");
		}

		_logger.LogInformation("Successfully resent OTP email to: {@Context}", logContext);

		return isSent;
	}

	public async Task<bool> ResendOtpAsync(OtpVerification otpVerification)
	{

		var logContext = new
		{
			Action = "ResendingAutoOTPToUser",
			Step = "StartResending",
			Email = otpVerification.Email,
			Timestamp = DateTime.UtcNow
		};

		var otp = _otpService.GenerateOtp();


		otpVerification.OtpCodeHash = _hashService.Hash(otp);

		otpVerification.ExpiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryMinutes);

		var isUpdated = await _authRepository.UpdateVerificationCodeAsync(otpVerification);

		if (!isUpdated)
		{
			_logger.LogError("Failed to update OTP record for email: {@Context}", logContext);
			throw new Exception("Failed to update OTP record.");
		}


		if (otp == null)
		{
			_logger.LogError("Failed to generate OTP for email: {@Context}", logContext);
			throw new Exception("Failed to generate OTP.");
		}


		var HashOTP = _hashService.Hash(otp);

		_logger.LogInformation("Hashed OTP for email: {@Context}", logContext);

		if (HashOTP == null)
		{
			_logger.LogError("Failed to hash OTP for email: {@Context}", logContext);
			throw new Exception("Failed to hash OTP.");
		}

		var name = $"{otpVerification.FirstName} {otpVerification.LastName}";

		var otpBody = _emailService.SendOtpBody(name, otp);

		var isSent = await _emailService.SendEmailAsync(
			toEmail: otpVerification.Email,
			subject: "Your OTP Code",
			body: otpBody
		);

		if (!isSent)
		{
			_logger.LogError("Failed to send OTP email to: {@Context}", logContext);
			throw new Exception("Failed to send OTP email.");
		}

		_logger.LogInformation("Successfully sent OTP email to: {@Context}", logContext);

		return true;
	}

	public async Task<bool> IsOtpSessionValidAsync(Guid userId, string email)
	{
		var logContext = new
		{
			Action = "VerifyingIfValidUser",
			Step = "StartVerifying",
			Email = email,
			UserId = userId,
			Timestamp = DateTime.UtcNow
		};


		_logger.LogInformation("Checking if OTP is valid for user: {@Context}", logContext);

		var userDetail = new OtpVerificationRequestDTO(userId, email);

		var existingOtpRecord = await _authRepository.OtpVerificationUserData(userDetail);

		if (existingOtpRecord == null)
		{
			_logger.LogWarning("No OTP record found for email: {@Context}", logContext);
			return false;
		}

		if (existingOtpRecord.IsUsed)
		{
			_logger.LogWarning("OTP already used for email: {@Context}", logContext);
			return false;
		}

		_logger.LogInformation("OTP is valid for user: {@Context}", logContext);

		return true;
	}


}
