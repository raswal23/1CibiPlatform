namespace FrontendWebassembly.Services.Auth.Implementation;

public class AuthService : IAuthService
{
	private HttpClient _httpClient;
	private readonly LocalStorageService _localStorageService;
	private readonly ILogger<AuthService> _logger;

	private readonly string _userNameKey;
	private readonly string _userIdKey;
	private readonly string _appIdKey;
	private readonly string _subMenuKey;
	private readonly string _roleIdKey;

	public AuthService(IHttpClientFactory httpClientFactory,
		LocalStorageService localStorageService,
		ILogger<AuthService> logger)
	{
		this._httpClient = httpClientFactory.CreateClient("API");
		this._localStorageService = localStorageService;
		this._logger = logger;


		this._userNameKey = "Name";
		this._userIdKey = "UserId";
		this._appIdKey = "AppId";
		this._subMenuKey = "SubMenuId";
		this._roleIdKey = "RoleId";
	}

	public async Task<AuthResponseDTO> Login(LoginCred cred)
	{
		_logger.LogDebug("Starting login request for {Email}", cred.Email);

		var payload = new
		{
			loginWebCred = new
			{
				Username = cred.Email,
				Password = cred.Password,
				IsRememberMe = cred.IsRememberMe
			}
		};

		_logger.LogDebug("Sending POST to /token/web/generatetoken for user: {Email}", cred.Email);

		var response = await _httpClient.PostAsJsonAsync("/token/web/generatetoken", payload);
		_logger.LogDebug("Received response: {Status} {Reason}", (int)response.StatusCode, response.ReasonPhrase);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Login failed for {Email}. Reading error content...", cred.Email);

			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

			_logger.LogError("Login error detail: {Detail}", errorContent?.Detail);
			return new AuthResponseDTO(Guid.Empty, string.Empty, errorContent?.Detail ?? "", "Error");
		}

		_logger.LogInformation("Login successful for {Email}. Reading success content...", cred.Email);

		var successContent = await response.Content.ReadFromJsonAsync<CredResponseDTO>();

		_logger.LogDebug("Storing user info in local storage for UserId: {UserId}", successContent?.UserId);
		await this.SetLocalstorage(successContent!);

		_logger.LogInformation("User {Email} logged in successfully with UserId: {UserId}", cred.Email, successContent!.UserId);

		return new AuthResponseDTO(successContent.UserId, successContent.AccessToken, string.Empty, string.Empty);
	}

	protected virtual async Task SetLocalstorage(CredResponseDTO credResponseDTO)
	{
		// Store UserId and Username in local storage
		await this._localStorageService.SetItemAsync(_userIdKey, credResponseDTO.UserId.ToString());
		await this._localStorageService.SetItemAsync(_userNameKey, credResponseDTO.Name);
		await this._localStorageService.SetItemAsync(_appIdKey, JsonSerializer.Serialize(credResponseDTO.Appid));
		await this._localStorageService.SetItemAsync(_subMenuKey, JsonSerializer.Serialize(credResponseDTO.SubMenuid));
		await this._localStorageService.SetItemAsync(_roleIdKey, JsonSerializer.Serialize(credResponseDTO.RoleId));
	}

	public async Task<bool> IsAuthenticated()
	{
		var response = await _httpClient.GetAsync("/auth/isAuthenticated");

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogError("Something went wrong contact IT Team. Response: {Response}", response);
			return false;
		}

		var successContent = await response.Content.ReadFromJsonAsync<IsAuthenticatedDTO>();

		if (successContent!.isAuthenticated == false)
		{
			_logger.LogInformation("User is not authenticated");
			return false;

		}

		return true;
	}

	public async Task<bool> Logout()
	{
		_logger.LogDebug("Starting logout request...");

		var userId = await _localStorageService.GetItemAsync<Guid>(_userIdKey);

		if (userId == Guid.Empty)
		{
			_logger.LogWarning("UserId not found in local storage. Cannot proceed with logout.");
			return false;
		}

		var payload = new
		{
			logoutDTO = new
			{
				UserId = userId,
				RevokeReason = "User Logged out"
			}
		};

		var response = await _httpClient.PostAsJsonAsync("/auth/logout", payload);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogError("Logout failed. Response: {Response}", JsonSerializer.Serialize(response));
			return false;
		}

		var successContent = await response.Content.ReadFromJsonAsync<LogoutResponseDTO>();

		if (successContent!.isLoggedOut == false)
		{
			_logger.LogWarning("User is not logged out");
			return false;
		}


		await this._localStorageService.ClearAsync();

		_logger.LogInformation("Logout successful.");

		return true;
	}

	public async Task<RegisterResponseDTO> Register(RegisterRequestDTO registerRequestDTO)
	{
		_logger.LogDebug("Starting registration request for {Email}...", registerRequestDTO.Email);

		var payload = new
		{
			register = new
			{
				email = registerRequestDTO.Email,
				passwordHash = registerRequestDTO.PasswordHash,
				firstName = registerRequestDTO.FirstName,
				lastName = registerRequestDTO.LastName,
				middleName = registerRequestDTO.MiddleName
			}
		};

		_logger.LogDebug("Sending POST to /auth/register for email: {Email}", registerRequestDTO.Email);

		var response = await _httpClient.PostAsJsonAsync("/auth/register", payload);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Registration failed for {Email}. Reading error content...", registerRequestDTO.Email);

			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

			return new RegisterResponseDTO(Guid.Empty, string.Empty, errorContent!.Detail);
		}

		_logger.LogInformation("Registration successful for {Email}. Reading success content...", registerRequestDTO.Email);

		var successContent = await response.Content.ReadFromJsonAsync<OtpVerificationResponseDTO>();

		return new RegisterResponseDTO(successContent!.OtpId, successContent!.Email, string.Empty);
	}

	public async Task<OtpSessionResponseDTO> IsOtpSessionValid(OtpSessionRequestDTO otpRequestDTO)
	{

		_logger.LogDebug("Starting OTP validation request for UserId: {UserId}, Email: {Email}", otpRequestDTO.userId, otpRequestDTO.email);

		var payload = new
		{
			OtpVerificationRequestDTO = new
			{
				userId = otpRequestDTO.userId,
				email = otpRequestDTO.email
			}
		};

		var response = await _httpClient.PostAsJsonAsync("/auth/validate/otp", payload);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("OTP validation failed for {Email}. Reading error content...", otpRequestDTO.email);

			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

			return new OtpSessionResponseDTO(false, errorContent!.Detail);
		}

		_logger.LogInformation("OTP validation successful for {Email}. Reading success content...", otpRequestDTO.email);

		var successContent = await response.Content.ReadFromJsonAsync<OtpVerificationSessionResponseDTO>();

		return new OtpSessionResponseDTO(successContent!.isOtpSessionValid, string.Empty);
	}

	public async Task<OtpSessionResponseDTO> OtpVerification(OtpVerificationRequestDTO otpVerificationRequestDTO)
	{
		_logger.LogDebug("Starting OTP verification request for Email: {Email}", otpVerificationRequestDTO.Email);

		var payload = new
		{
			OtpRequestDTO = new
			{
				Email = otpVerificationRequestDTO.Email,
				Otp = otpVerificationRequestDTO.Otp,
			}
		};

		_logger.LogDebug("Sending POST to /auth/verify/otp for Email: {Email}", otpVerificationRequestDTO.Email);

		var response = await _httpClient.PostAsJsonAsync("/auth/verify/otp", payload);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("OTP verification failed for {Email}. Reading error content...", otpVerificationRequestDTO.Email);
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			return new OtpSessionResponseDTO(false, errorContent!.Detail);
		}

		_logger.LogInformation("OTP verification successful for {Email}.");

		return new OtpSessionResponseDTO(true, string.Empty);
	}

	public async Task<OTPResendResponseDTO> OtpResendAsync(OTPResendRequestDTO otpResendRequestDTO)
	{
		_logger.LogDebug("Starting resending email for OTP for UserId: {UserId}, Email: {Email}", otpResendRequestDTO.userId, otpResendRequestDTO.email);

		var payload = new
		{
			OtpVerificationRequestDto = new
			{
				userId = otpResendRequestDTO.userId,
				email = otpResendRequestDTO.email
			}
		};

		_logger.LogDebug("Sending POST to /auth/resend-otp for UserId: {UserId}, Email: {Email}", otpResendRequestDTO.userId, otpResendRequestDTO.email);

		var response = await _httpClient.PostAsJsonAsync("/auth/resend-otp", payload);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Resending OTP failed for {Email}. Reading error content...", otpResendRequestDTO.email);
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			return new OTPResendResponseDTO(false, errorContent!.Detail);
		}

		_logger.LogInformation("Resending OTP successful for {Email}.", otpResendRequestDTO.email);

		return new OTPResendResponseDTO(true, string.Empty);
	}

	public async Task<SendEmailForgotPasswordResponseDTO> ForgotPasswordSendEmail(SendEmailForgotPasswordRequestDTO sendEmailForgotPasswordRequestDTO)
	{
		_logger.LogDebug("Starting Forgot Password - Get User ID request for Email: {Email}...", sendEmailForgotPasswordRequestDTO.email);

		var payload = new
		{
			sendForgotPasswordEmailRequestDTO = new
			{
				email = sendEmailForgotPasswordRequestDTO.email
			}
		};

		_logger.LogDebug("Sending POST to /auth/forgot-password-email-send for Email: {Email}", sendEmailForgotPasswordRequestDTO.email);

		var response = await _httpClient.PostAsJsonAsync("/auth/forgot-password-email-send", payload);


		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Forgot Password - Get User ID failed for {Email}. Reading error content...", sendEmailForgotPasswordRequestDTO.email);
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			_logger.LogError("Error: {Detail}", errorContent!.Detail);
			return new SendEmailForgotPasswordResponseDTO(errorContent.Detail);
		}

		_logger.LogInformation("Forgot Password - Get User ID successful for {Email}. Reading success content...", sendEmailForgotPasswordRequestDTO.email);

		var successContent = await response.Content.ReadFromJsonAsync<SendEmailForgotPasswordResponseDTO>();

		return successContent!;

	}

	public async Task<IsChangePasswordTokenValidResponseDTO> IsForgotPasswordTokenValid(ForgotPasswordTokenRequestDTO forgotPasswordTokenRequestDTO)
	{
		_logger.LogDebug("Starting Forgot Password - Validate Token request for TokenHash: {TokenHash}...", forgotPasswordTokenRequestDTO.tokenHash);

		var payload = new
		{
			forgotPasswordTokenRequestDTO = new
			{
				tokenHash = forgotPasswordTokenRequestDTO.tokenHash
			}
		};

		_logger.LogDebug("Sending POST to /auth/forgot-password/is-change-password-token-valid for TokenHash: {TokenHash}", forgotPasswordTokenRequestDTO.tokenHash);

		var response = await _httpClient.PostAsJsonAsync("/auth/forgot-password/is-change-password-token-valid", payload);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Forgot Password - Validate Token failed. Reading error content...");
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			_logger.LogError("Error: {Detail}", errorContent!.Detail);
			return new IsChangePasswordTokenValidResponseDTO(false, errorContent.Detail);
		}

		_logger.LogInformation("Forgot Password - Validate Token successful. Reading success content...");

		var successContent = await response.Content.ReadFromJsonAsync<IsChangePasswordTokenValidResponseDTO>();

		return successContent!;

	}

	public async Task<UpdatePasswordResponseDTO> UpdatePassword(UpdatePasswordRequestDTO updatePasswordRequestDTO)
	{
		_logger.LogDebug("Starting Update Password request", updatePasswordRequestDTO);

		var payload = new
		{
			updatePasswordRequestDTO = new
			{
				hashToken = updatePasswordRequestDTO.hashToken,
				newPassword = updatePasswordRequestDTO.newPassword
			}
		};
		_logger.LogDebug("Sending POST to /auth/forgot-password/change-password", updatePasswordRequestDTO);

		var response = await _httpClient.PostAsJsonAsync("/auth/forgot-password/change-password", payload);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
			_logger.LogError("Error: {Detail}", errorContent!.Detail);
			return new UpdatePasswordResponseDTO(false, errorContent.Detail);
		}
		_logger.LogInformation("Update Password successful. Reading success content...");

		var successContent = await response.Content.ReadFromJsonAsync<UpdatePasswordResponseDTO>();

		return successContent!;

	}
}
