namespace FrontendWebassembly.Services.ATS.Interface;

public interface IApplicationFormStateService
{
	Task SaveAsync(ApplicationFormState state);
	Task<ApplicationFormState?> LoadAsync();
	Task ClearAsync();
	Task<bool> HasSavedStateAsync();
}
