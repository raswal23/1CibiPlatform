namespace Auth.Data.Repository;

public interface IApplicationRepository
{
	Task<List<ApplicationsDTO>> GetApplicationsPageAsync(string? searchTerm, int? afterAppId, int take, CancellationToken cancellationToken);
	Task<long> CountApplicationsAsync(string? searchTerm, CancellationToken cancellationToken);
	Task<AuthApplication> GetApplicationAsync(int applicationId);
	Task<bool> AddApplicationAsync(AddApplicationDTO application);
	Task<AuthApplication> EditApplicationAsync(AuthApplication application);
	Task<bool> DeleteApplicationAsync(AuthApplication application);
}
