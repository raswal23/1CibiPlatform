namespace Auth.Services;

public class ApplicationService : IApplicationService
{
	private readonly IApplicationRepository _authRepository;
	private readonly ILogger<ApplicationService> _logger;

	public ApplicationService(IApplicationRepository authRepository,
					   ILogger<ApplicationService> logger)
	{
		_authRepository = authRepository;
		_logger = logger;
	}

	public Task<PaginatedResult<ApplicationsDTO>> GetApplicationsAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetApplications",
			Step = "FetchingApplications",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching application with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_authRepository.GetApplicationsAsync(paginationRequest, cancellationToken) :
			_authRepository.SearchApplicationsAsync(paginationRequest, cancellationToken);
	}

	public async Task<bool> DeleteApplicationAsync(int AppId)
	{
		var logContext = new
		{
			Action = "DeleteApplication",
			Step = "FetchForDelete",
			AppId,
			Timestamp = DateTime.UtcNow
		};

		var application = await _authRepository.GetApplicationAsync(AppId);
		if (application == null)
		{
			_logger.LogError("{AppId} was not found during delete operation: {@Context}", AppId, logContext);
			throw new NotFoundException($"Application with ID {AppId} was not found.");
		}
		var isDeleted = await _authRepository.DeleteApplicationAsync(application); 
		return isDeleted;
	}

	public async Task<bool> AddApplicationAsync(AddApplicationDTO application)
	{
		var isAdded = await _authRepository.AddApplicationAsync(application);
		return isAdded;
	}

	public async Task<ApplicationDTO> EditApplicationAsync(EditApplicationDTO applicationDTO)
	{
		var logContext = new
		{
			Action = "EditApplication",
			Step = "FetchForUpdate",
			AppId = applicationDTO.AppId,
			Timestamp = DateTime.UtcNow
		};

		var existingApplication = await _authRepository.GetApplicationAsync(applicationDTO.AppId);
		if (existingApplication == null)
		{
			_logger.LogError("{AppId} was not found during update operation: {@Context}", applicationDTO.AppId, logContext);
			throw new NotFoundException($"Application with ID {applicationDTO.AppId} was not found.");
		}

		existingApplication.AppName = applicationDTO.AppName!;
		existingApplication.Description = applicationDTO.Description;
		existingApplication.IsActive = applicationDTO.IsActive;

		var application = await _authRepository.EditApplicationAsync(existingApplication);
		return application.Adapt<ApplicationDTO>();
	}
}
