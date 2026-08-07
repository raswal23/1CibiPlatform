namespace FrontendWebassembly.Services.ATS.Interface;

public interface IApplicationFormStateService
{
	Task SaveAsync(ApplicationFormState state);
	Task<ApplicationFormState?> LoadAsync(Guid emailInvitationId);
	Task ClearAsync(Guid emailInvitationId);
	Task CleanupExpiredAsync();
	Task<bool> HasSavedStateAsync(Guid emailInvitationId);
}
