namespace ATS.Data.Repository;

public interface IPackageRepository
{
	Task<List<PackageDetailsDTO>> GetPackagesPageAsync(string? searchTerm, int? clientId, string? afterPackageName, int take, CancellationToken cancellationToken);
	Task<long> CountPackagesAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken);
	Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken);
	Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken);
	Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken);

	/// <summary>
	/// Refreshes the denormalised package name carried by every order and bulk file that
	/// references this package, after it has been renamed. Returns how many rows of each
	/// were touched.
	/// </summary>
	Task<(int Orders, int BulkFiles)> RelabelPackageOnOrdersAsync(
		int packageId,
		string packageName,
		CancellationToken cancellationToken);
}
