namespace Auth.Services;
public interface IApplicationService
{
	Task<KeysetPaginatedResult<ApplicationsDTO>> GetApplicationsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> DeleteApplicationAsync(int AppId);

	Task<ApplicationDTO> EditApplicationAsync(EditApplicationDTO applicationDTO);
	Task<bool> AddApplicationAsync(AddApplicationDTO application);
}
