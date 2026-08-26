namespace FrontendWebassembly.Services.Auth.Implementation;

public class UserProfileService : IUserProfileService
{
	private const string UserNameKey = "Name";

	private readonly HttpClient _httpClient;
	private readonly LocalStorageService _localStorageService;
	private readonly ILogger<UserProfileService> _logger;

	public UserProfileService(
		IHttpClientFactory httpClientFactory,
		LocalStorageService localStorageService,
		ILogger<UserProfileService> logger)
	{
		_httpClient = httpClientFactory.CreateClient("API");
		_localStorageService = localStorageService;
		_logger = logger;
	}

	public async Task<ServiceResponse<UserProfileDTO>> GetMyProfileAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync("auth/getmyprofile", cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				var detail = await response.ReadErrorDetailAsync(cancellationToken);
				_logger.LogError("Failed to load the user profile: {Detail}", detail);

				return ServiceResponse<UserProfileDTO>.Failure(detail);
			}

			var envelope = await response.Content
				.ReadFromJsonAsync<UserProfileResponseDTO>(cancellationToken: cancellationToken);

			if (envelope?.Profile is null)
			{
				return ServiceResponse<UserProfileDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<UserProfileDTO>.Success(envelope.Profile);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			_logger.LogError(ex, "Unable to reach the server while loading the user profile.");

			return ServiceResponse<UserProfileDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<UserProfileDTO>> UpdateMyProfileAsync(
		UpdateUserProfileDTO profile,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.PatchAsJsonAsync(
				"auth/updatemyprofile",
				new { updateProfile = profile },
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				var detail = await response.ReadErrorDetailAsync(cancellationToken);
				_logger.LogError("Failed to update the user profile: {Detail}", detail);

				return ServiceResponse<UserProfileDTO>.Failure(detail);
			}

			var envelope = await response.Content
				.ReadFromJsonAsync<UserProfileResponseDTO>(cancellationToken: cancellationToken);

			if (envelope?.Profile is null)
			{
				return ServiceResponse<UserProfileDTO>.Failure("The server returned an empty response.");
			}

			// The greeting in the top bar reads this key, and the access token is
			// only reissued on refresh, so the stored display name is updated here.
			if (!string.IsNullOrWhiteSpace(envelope.Profile.FullName))
			{
				await _localStorageService.SetItemAsync(UserNameKey, envelope.Profile.FullName);
			}

			return ServiceResponse<UserProfileDTO>.Success(envelope.Profile);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			_logger.LogError(ex, "Unable to reach the server while updating the user profile.");

			return ServiceResponse<UserProfileDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
