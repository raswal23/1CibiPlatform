namespace FrontendWebassembly.Services.ATS.Implementation;

public sealed class ApplicationFormStateService : IApplicationFormStateService
{
	public const string StorageKey = "ats:application-form-drafts";
	private const string LegacyStorageKey = "ats:application-form-state";
	private static readonly TimeSpan DraftLifetime = TimeSpan.FromDays(1);

	private readonly LocalStorageService _localStorageService;

	public ApplicationFormStateService(LocalStorageService localStorageService)
	{
		_localStorageService = localStorageService;
	}

	public async Task SaveAsync(ApplicationFormState state)
	{
		var drafts = await LoadDraftsAsync();
		RemoveExpired(drafts, DateTime.UtcNow);
		state.LastModifiedAtUtc = DateTime.UtcNow;
		drafts[state.EmailInvitationId] = state;
		await SaveDraftsAsync(drafts);
	}

	public async Task<ApplicationFormState?> LoadAsync(Guid emailInvitationId)
	{
		var drafts = await LoadDraftsAsync();
		var changed = RemoveExpired(drafts, DateTime.UtcNow);
		if (changed)
			await SaveDraftsAsync(drafts);

		return drafts.GetValueOrDefault(emailInvitationId);
	}

	public async Task ClearAsync(Guid emailInvitationId)
	{
		var drafts = await LoadDraftsAsync();
		if (!drafts.Remove(emailInvitationId))
			return;

		await SaveDraftsAsync(drafts);
	}

	public async Task CleanupExpiredAsync()
	{
		// Remove the obsolete single-draft entry after migrating to invitation-scoped drafts.
		await _localStorageService.RemoveItemAsync(LegacyStorageKey);

		var drafts = await LoadDraftsAsync();
		if (RemoveExpired(drafts, DateTime.UtcNow))
			await SaveDraftsAsync(drafts);
	}

	public async Task<bool> HasSavedStateAsync(Guid emailInvitationId) =>
		await LoadAsync(emailInvitationId) is not null;

	private async Task<Dictionary<Guid, ApplicationFormState>> LoadDraftsAsync()
	{
		try
		{
			return await _localStorageService.GetItemAsync<Dictionary<Guid, ApplicationFormState>>(StorageKey) ?? [];
		}
		catch (JsonException)
		{
			await _localStorageService.RemoveItemAsync(StorageKey);
			return [];
		}
		catch (NotSupportedException)
		{
			await _localStorageService.RemoveItemAsync(StorageKey);
			return [];
		}
	}

	private Task SaveDraftsAsync(Dictionary<Guid, ApplicationFormState> drafts) =>
		drafts.Count == 0
			? _localStorageService.RemoveItemAsync(StorageKey)
			: _localStorageService.SetItemAsync(StorageKey, drafts);

	private static bool RemoveExpired(Dictionary<Guid, ApplicationFormState> drafts, DateTime utcNow)
	{
		var expiredIds = drafts
			.Where(entry => !entry.Value.LastModifiedAtUtc.HasValue ||
				utcNow - entry.Value.LastModifiedAtUtc.Value.ToUniversalTime() > DraftLifetime)
			.Select(entry => entry.Key)
			.ToArray();

		foreach (var id in expiredIds)
			drafts.Remove(id);

		return expiredIds.Length > 0;
	}
}
