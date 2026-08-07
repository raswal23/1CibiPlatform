namespace ATS.Services;

public class PackageManagementService : IPackageManagementService
{
	private readonly IATSRepository _atsRepository;
	private readonly ILogger<PackageManagementService> _logger;

	public PackageManagementService(IATSRepository atsRepository,
					   ILogger<PackageManagementService> logger)
	{
		_atsRepository = atsRepository;
		_logger = logger;
	}

	public Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetPackages",
			Step = "FetchingPackages",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching packages with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ?
			_atsRepository.GetPackagesAsync(paginationRequest, cancellationToken) :
			_atsRepository.SearchPackagesAsync(paginationRequest, cancellationToken);
	}

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "AddPackage",
			Step = "CreatingPackage",
			PackageName = packageDTO.PackageName,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding package: {@Context}", logContext);

		var isAdded = await _atsRepository.AddPackageAsync(packageDTO, cancellationToken);
		return isAdded;
	}

	public async Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "EditPackage",
			Step = "FetchForUpdate",
			PackageId = packageDTO.PackageId,
			Timestamp = DateTime.UtcNow
		};

		var existingPackage = await _atsRepository.GetPackageAsync(packageDTO.PackageId, cancellationToken);
		if (existingPackage == null)
		{
			_logger.LogError("{PackageId} was not found during update operation: {@Context}", packageDTO.PackageId, logContext);
			throw new NotFoundException($"Package with ID {packageDTO.PackageId} was not found.");
		}

		existingPackage.PackageName = packageDTO.PackageName.Trim();
		existingPackage.PackageDescription = packageDTO.PackageDescription.Trim();
		existingPackage.IsActive = packageDTO.IsActive;
		existingPackage.FollowUpEmail = packageDTO.FollowUpEmail;
		existingPackage.UpdatedAt = DateTime.UtcNow;

		var package = await _atsRepository.EditPackageAsync(existingPackage, cancellationToken);
		return package.Adapt<PackageDetailsDTO>();
	}
}
