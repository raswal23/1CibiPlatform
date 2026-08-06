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

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO)
	{
		var logContext = new
		{
			Action = "AddPackage",
			Step = "CreatingPackage",
			PackageName = packageDTO.PackageName,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding package: {@Context}", logContext);

		var isAdded = await _atsRepository.AddPackageAsync(packageDTO);
		return isAdded;
	}

	public async Task<PackageDetailsDTO> EditPackageAsync(EditPackageDTO packageDTO)
	{
		var logContext = new
		{
			Action = "EditPackage",
			Step = "FetchForUpdate",
			PackageId = packageDTO.PackageId,
			Timestamp = DateTime.UtcNow
		};

		var existingPackage = await _atsRepository.GetPackageAsync(packageDTO.PackageId);
		if (existingPackage == null)
		{
			_logger.LogError("{PackageId} was not found during update operation: {@Context}", packageDTO.PackageId, logContext);
			throw new NotFoundException($"Package with ID {packageDTO.PackageId} was not found.");
		}

		existingPackage.PackageName = packageDTO.PackageName!;
		existingPackage.IsActive = packageDTO.IsActive;

		var package = await _atsRepository.EditPackageAsync(existingPackage);
		return package.Adapt<PackageDetailsDTO>();
	}
}
