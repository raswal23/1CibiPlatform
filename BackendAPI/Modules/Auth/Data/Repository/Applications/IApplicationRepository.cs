namespace Auth.Data.Repository;

public interface IApplicationRepository
{
	Task<PaginatedResult<ApplicationsDTO>> GetApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<AuthApplication> GetApplicationAsync(int applicationId);
	Task<PaginatedResult<ApplicationsDTO>> SearchApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> AddApplicationAsync(AddApplicationDTO application);
	Task<AuthApplication> EditApplicationAsync(AuthApplication application);
	Task<bool> DeleteApplicationAsync(AuthApplication application);
}
