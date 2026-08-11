namespace ATS.Services;

public class PackageManagementService : IPackageManagementService
{
	private readonly IPackageRepository _packageRepository;
	private readonly ILogger<PackageManagementService> _logger;

	public PackageManagementService(IPackageRepository packageRepository,
					   ILogger<PackageManagementService> logger)
	{
		_packageRepository = packageRepository;
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
			_packageRepository.GetPackagesAsync(paginationRequest, cancellationToken) :
			_packageRepository.SearchPackagesAsync(paginationRequest, cancellationToken);
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

		var isAdded = await _packageRepository.AddPackageAsync(packageDTO, cancellationToken);
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

		var existingPackage = await _packageRepository.GetPackageAsync(packageDTO.PackageId, cancellationToken);
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

		var package = await _packageRepository.EditPackageAsync(existingPackage, cancellationToken);
		return package.Adapt<PackageDetailsDTO>();
	}
}
