namespace FrontendWebassembly.Services.Auth.Implementation;

public class RefreshTokenService : IRefreshTokenService
{
	private HttpClient _httpClient;
	private readonly LocalStorageService _localStorageService;
	private readonly IConfiguration _configuration;
	private readonly ILogger<RefreshTokenService> _logger;

	private readonly string _userNameKey;
	private readonly string _userIdKey;
	private readonly string _appIdKey;
	private readonly string _subMenuKey;
	private readonly string _roleIdKey;


	public RefreshTokenService(IHttpClientFactory httpClientFactory,
		LocalStorageService localStorageService,
		IConfiguration configuration,
		ILogger<RefreshTokenService> logger)
	{
		_httpClient = httpClientFactory.CreateClient("RefreshAPI");
		this._localStorageService = localStorageService;
		this._configuration = configuration;
		this._logger = logger;

		this._userNameKey = "Name";
		this._userIdKey = "UserId";
		this._appIdKey = "AppId";
		this._subMenuKey = "SubMenuId";
		this._roleIdKey = "RoleId";
	}


	public async Task<AuthResponseDTO> GetNewAccessAndRefreshToken()
	{
		_logger.LogDebug("Starting token refresh request...");

		var response = await _httpClient.PostAsync("/token/web/getnewaccesstoken", null);

		_logger.LogDebug("Received response: {Status} {Reason}", (int)response.StatusCode, response.ReasonPhrase);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Getting new token failed. Reading error content...");

			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

			_logger.LogError("Could not parse JSON error. Detail: {Detail}", errorContent!.Detail);
			return new AuthResponseDTO(Guid.Empty, string.Empty, errorContent.Detail, "Error");
		}

		_logger.LogInformation("Getting new token successful. Reading success content...");

		var successContent = await response.Content.ReadFromJsonAsync<CredResponseDTO>();

		_logger.LogDebug("Storing user info in local storage for UserId: {UserId}", successContent?.UserId);
		await this.SetLocalstorage(successContent!);


		return new AuthResponseDTO(successContent!.UserId, string.Empty, string.Empty, string.Empty);
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

	public async Task<bool> Logout()
	{
		_logger.LogDebug("Starting logout request...");

		var response = await _httpClient.PostAsync("/auth/logout", null);

		if (!response.IsSuccessStatusCode)
		{
			_logger.LogError("Something went wrong call the IT Team for further support {Response}", JsonSerializer.Serialize(response));
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

}
