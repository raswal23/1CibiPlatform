namespace FrontendWebassembly.Services.ATS.Implementation;

public sealed class ApplicationFormStateService : IApplicationFormStateService
{
	public const string StorageKey = "ats:application-form-state";

	private readonly LocalStorageService _localStorageService;

	public ApplicationFormStateService(LocalStorageService localStorageService)
	{
		_localStorageService = localStorageService;
	}

	public Task SaveAsync(ApplicationFormState state) =>
		_localStorageService.SetItemAsync(StorageKey, state);

	public async Task<ApplicationFormState?> LoadAsync()
	{
		try
		{
			return await _localStorageService.GetItemAsync<ApplicationFormState>(StorageKey);
		}
		catch (JsonException)
		{
			await ClearAsync();
			return null;
		}
		catch (NotSupportedException)
		{
			await ClearAsync();
			return null;
		}
	}

	public Task ClearAsync() => _localStorageService.RemoveItemAsync(StorageKey);

	public Task<bool> HasSavedStateAsync() =>
		_localStorageService.ContainsKeyAsync(StorageKey);
}
