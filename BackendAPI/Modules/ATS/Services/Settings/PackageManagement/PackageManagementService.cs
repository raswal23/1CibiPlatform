namespace ATS.Services.Settings.PackageManagement;

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

	public async Task<KeysetPaginatedResult<PackageDetailsDTO>> GetPackagesAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken,
		int? clientId = null)
	{
		var logContext = new
		{
			Action = "GetPackages",
			Step = "FetchingPackages",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching packages with pagination: {@Context}", logContext);

		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 1);
		var afterPackageName = fields?[0];
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _packageRepository.GetPackagesPageAsync(
			paginationRequest.SearchTerm, 
			clientId, 
			afterPackageName, 
			pageSize + 1, 
			cancellationToken);
		var (items, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore ? CursorCodec.Encode(items[^1].PackageName) : null;
		long? totalCount = afterPackageName is null
			? await _packageRepository.CountPackagesAsync(paginationRequest.SearchTerm, clientId, cancellationToken)
			: null;

		return new KeysetPaginatedResult<PackageDetailsDTO>(items, nextCursor, totalCount);
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

		var newName = packageDTO.PackageName.Trim();

		// Orders reference the package by id, so a rename cannot break them - but they
		// also carry the name as a display label, which would go stale.
		var wasRenamed = !string.Equals(existingPackage.PackageName, newName, StringComparison.Ordinal);

		existingPackage.PackageName = newName;
		existingPackage.PackageDescription = packageDTO.PackageDescription.Trim();
		existingPackage.IsActive = packageDTO.IsActive;
		existingPackage.FollowUpEmail = packageDTO.FollowUpEmail;
		existingPackage.UpdatedAt = DateTime.UtcNow;

		var package = await _packageRepository.EditPackageAsync(existingPackage, cancellationToken);

		if (wasRenamed)
		{
			// Renames are rare, so this is one statement per table rather than a batched
			// job. NeedsProjection is raised inside, so the applicant search rows pick
			// the new name up on the projection job's next pass.
			var relabelled = await _packageRepository.RelabelPackageOnOrdersAsync(
				package.PackageId,
				newName,
				cancellationToken);

			_logger.LogInformation(
				"Package {PackageId} renamed; refreshed the label on {OrderCount} order(s) and {FileCount} bulk file(s): {@Context}",
				package.PackageId,
				relabelled.Orders,
				relabelled.BulkFiles,
				logContext);
		}

		return package.Adapt<PackageDetailsDTO>();
	}
}
