namespace Auth.Services
{
	public class RefreshTokenService : IRefreshTokenService
	{
		private readonly IRefreshTokenRepository _authRepository;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IJWTService _jWTService;
		private readonly IAtsAccessClaimsProvider _atsAccessClaimsProvider;
		private readonly IConfiguration _configuration;
		private readonly ILogger<RefreshTokenService> _logger;

		private readonly string _httpCookieOnlyKey;
		private readonly double _expiryinMinutesKey;
		private readonly string _refreshTokenKey;
		private readonly bool _isHttps;
		private readonly int _cookieExpiryinDaysKey;
		private readonly double _expiryinMinutesKeyInCookie;
		private readonly string _refreshTokenExpirationKey;
		private readonly int _httpCookieOnlyRefreshTokenInDays;

		public RefreshTokenService(
			IRefreshTokenRepository authRepository,
			IHttpContextAccessor httpContextAccessor,
			IJWTService jWTService,
			IAtsAccessClaimsProvider atsAccessClaimsProvider,
			IConfiguration configuration,
			ILogger<RefreshTokenService> logger)
		{
			this._authRepository = authRepository;
			this._httpContextAccessor = httpContextAccessor;
			this._jWTService = jWTService;
			this._atsAccessClaimsProvider = atsAccessClaimsProvider;
			this._configuration = configuration;
			this._logger = logger;

			_httpCookieOnlyKey = _configuration.GetValue<string>("HttpCookieOnlyKey") ?? "";
			_httpCookieOnlyRefreshTokenInDays = _configuration.GetValue<int>("AuthWeb:HttpCookieOnlyRefreshTokenInDays", 60);
			_expiryinMinutesKey = double.Parse(_configuration.GetSection("Jwt:ExpiryInMinutes").Value! ?? "");
			_expiryinMinutesKeyInCookie = _expiryinMinutesKey + 30;
			_refreshTokenKey = _configuration.GetSection("AuthWeb:AuthWebHttpCookieOnlyKey").Value! ?? "";
			_isHttps = bool.Parse(_configuration.GetSection("AuthWeb:isHttps").Value!);
			_cookieExpiryinDaysKey = _configuration.GetValue<int>("AuthWeb:CookieExpiryInDayIsRememberMe");
		}


		public virtual (string, string) GenerateRefreshToken()
		{
			// Generate random token
			var randomNumber = new byte[64];
			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(randomNumber);


			var token = Convert.ToBase64String(randomNumber)
				.TrimEnd('=')
				.Replace('+', '-')
				.Replace('/', '_');

			// Hash for storage
			var hashedToken = HashToken(token);
			return (token, hashedToken);
		}

		public virtual string HashToken(string token)
		{
			using var sHA512 = SHA512.Create();
			var hashBytes = sHA512.ComputeHash(Encoding.UTF8.GetBytes(token));
			return Convert.ToBase64String(hashBytes);
		}

		public virtual bool ValidateHashToken(string providedToken, string storedHash)
		{
			var decodedToken = System.Net.WebUtility.UrlDecode(providedToken);

			var providedHash = HashToken(decodedToken);

			return CryptographicOperations.FixedTimeEquals(
				Convert.FromBase64String(providedHash),
				Convert.FromBase64String(storedHash)
			);
		}

		public virtual Task<string> RevokeTokenAsync()
		{
			throw new NotImplementedException();
		}

		public virtual async Task<LoginResponseWebDTO> GetNewAccessTokenAsync()
		{
			var logContext = new
			{
				Action = "GettingNewAccessToken",
				Step = "StartGetting",
				Timestamp = DateTime.UtcNow
			};

			var rawRefreshToken = _httpContextAccessor.HttpContext?
			   .Request.Cookies[_refreshTokenKey];

			if (string.IsNullOrWhiteSpace(rawRefreshToken))
			{
				_logger.LogWarning("Refresh token cookie is missing {@Context}", logContext);
				throw new UnauthorizedAccessException("Invalid refresh token.");
			}

			var refreshTokenHash = HashToken(rawRefreshToken);
			var storedRefreshToken = await _authRepository
				.FindActiveRefreshTokenByHashAsync(refreshTokenHash);

			if (storedRefreshToken is null)
			{
				_logger.LogWarning("Refresh token is invalid or expired {@Context}", logContext);
				throw new UnauthorizedAccessException("Invalid refresh token.");
			}

			var userId = storedRefreshToken.UserId;
			var userData = await _authRepository.GetNewUserDataAsync(userId);

			if (userData == null)
			{
				_logger.LogWarning("Refresh Token is not found or invalid {@Context}", logContext);
				throw new NotFoundException("Refresh Token is not found.");
			}

			var roleId = userData.roleId;
			var appId = userData.AppId;
			var subMenuId = userData.SubMenuId;


			// Generate the replacement tokens only after the cookie has been
			// authenticated and the user id has been derived from its DB record.
			var loginDTO = userData.Adapt<LoginDTO>();
			var atsClaims = await _atsAccessClaimsProvider.GetClaimsAsync(userId);
			loginDTO = loginDTO with
			{
				AtsClientId = atsClaims?.AtsClientId,
				AtsRoleId = atsClaims?.AtsRoleId
			};
			string jwtToken = this._jWTService.GetAccessToken(loginDTO);

			var (newRefreshToken, newRefreshTokenHash) = this.GenerateRefreshToken();

			var name = !string.IsNullOrEmpty(userData.MiddleName) ?
				  $"{userData.FirstName} {userData.MiddleName} {userData.LastName}" :
				  $"{userData.FirstName} {userData.LastName}";

			storedRefreshToken.TokenHash = newRefreshTokenHash;
			storedRefreshToken.CreatedAt = DateTime.UtcNow;
			storedRefreshToken.ExpiresAt = DateTime.UtcNow.AddDays(_httpCookieOnlyRefreshTokenInDays);
			storedRefreshToken.IsActive = true;

			var isUpdated = await _authRepository.UpdateRefreshTokenAsync(storedRefreshToken);

			if (!isUpdated)
			{
				_logger.LogError("Failed to update refresh token for user: {@Context}", logContext);
				throw new Exception("Failed to update refresh token.");
			}

			SetAccessTokenCookie(jwtToken);
			SetRefreshTokenCookie(newRefreshToken, false);

			// reuse existing refresh token if not expired
			return new LoginResponseWebDTO(
				userData.Id.ToString()!,
				jwtToken,
				newRefreshToken!,
				name,
				"bearer",
				ExpireInMinutes(),
				appId,
				subMenuId,
				roleId,
				DateTime.Now.ToString(),
				DateTime.Now.AddMinutes(_expiryinMinutesKey).ToString()
			);
		}

		protected virtual string? GetAccessTokenFromCookie()
		{
			var accessToken = _httpContextAccessor.HttpContext!.Request.Cookies[_httpCookieOnlyKey!];
			return accessToken;
		}

		protected virtual void SetAccessTokenCookie(
			string accessToken)
		{
			// set httpcookieonly
			var cookieAccessTokenOptions = new CookieOptions
			{
				HttpOnly = true,
				Secure = _isHttps,
				SameSite = SameSiteMode.None,
				Expires = DateTime.UtcNow.AddMinutes(_expiryinMinutesKeyInCookie)
			};


			_httpContextAccessor.HttpContext!.Response.Cookies.Append(_httpCookieOnlyKey!, accessToken, cookieAccessTokenOptions);
		}


		protected virtual void SetRefreshTokenCookie(
			  string refreshToken,
			  bool isRememberMe)
		{
			// set httpcookieonly

			var cookieRefreshTokenOptions = new CookieOptions
			{
				HttpOnly = true,
				Secure = _isHttps,
				SameSite = SameSiteMode.Lax,
				Expires = isRememberMe ? DateTime.UtcNow.AddDays(_cookieExpiryinDaysKey) : DateTime.UtcNow.AddDays(_httpCookieOnlyRefreshTokenInDays)
			};


			_httpContextAccessor.HttpContext!.Response.Cookies.Append(_refreshTokenKey!, refreshToken, cookieRefreshTokenOptions);
		}

		protected virtual int ExpireInMinutes()
		{
			double configTime = double.Parse(_configuration.GetSection("Jwt:ExpiryInMinutes").Value!);

			var expireIn = (int)(configTime * 60);

			return expireIn;
		}
	}
}
