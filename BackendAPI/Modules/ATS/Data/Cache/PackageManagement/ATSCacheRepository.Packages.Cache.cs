namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public Task<List<PackageDetailsDTO>> GetPackagesPageAsync(string? searchTerm, int? clientId, bool? autoChasing, string? afterPackageName, int take, CancellationToken cancellationToken)
	{
		if (afterPackageName is not null)
			return _atsRepository.GetPackagesPageAsync(searchTerm, clientId, autoChasing, afterPackageName, take, cancellationToken);

		var key = $"package_v5_client_{clientId?.ToString() ?? "all"}_chasing_{autoChasing?.ToString() ?? "all"}_first_take_{take}_search_{searchTerm}";
		return _hybridCache.GetOrCreateAsync<List<PackageDetailsDTO>>(
			key, async token => await _atsRepository.GetPackagesPageAsync(searchTerm, clientId, autoChasing, null, take, token),
			tags: [CacheTags.Package], cancellationToken: cancellationToken).AsTask();
	}

	public Task<long> CountPackagesAsync(string? searchTerm, int? clientId, bool? autoChasing, CancellationToken cancellationToken) =>
		_hybridCache.GetOrCreateAsync<long>(
			$"package_v5_client_{clientId?.ToString() ?? "all"}_chasing_{autoChasing?.ToString() ?? "all"}_count_search_{searchTerm}",
			async token => await _atsRepository.CountPackagesAsync(searchTerm, clientId, autoChasing, token),
			tags: [CacheTags.Package], cancellationToken: cancellationToken).AsTask();

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.AddPackageAsync(packageDTO, cancellationToken);
		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		return result;
	}

	public Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_atsRepository.GetPackageAsync(packageId, cancellationToken);

	// Deactivation guard — must always see the current assignments, never a cached count.
	public Task<int> CountActiveClientsUsingPackageAsync(int packageId, CancellationToken cancellationToken) =>
		_atsRepository.CountActiveClientsUsingPackageAsync(packageId, cancellationToken);

	public async Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.EditPackageAsync(packageDetails, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.Client, cancellationToken);

		// FollowUpEmail is read live off PackageDetails by the report rows (see
		// ATSRepository.BuildReportRowsQuery) and baked into the cached page as the board's
		// "Follow-ups Left", so an edit that changes the reminder count leaves that column
		// stale for however long the entry lives. The chaser honours the new value on its
		// very next pass - its stop condition joins PackageDetails directly - so without
		// this the board and the chaser disagree, which is the one thing
		// ReportService.CalculateFollowUpEmailsRemaining exists to prevent.
		//
		// Not conditional on the count having changed: an edit here is a rare admin action,
		// and comparing against the pre-update value would mean reading the row back purely
		// to decide whether to drop a tag. Previously only the rename path invalidated this,
		// so a package whose reminders were retuned without being renamed kept reporting the
		// old schedule.
		await _hybridCache.RemoveByTagAsync(CacheTags.Report, cancellationToken);

		return result;
	}

	// A rename changes the package name shown on every order that references it, so the
	// report and withdrawn lists have to be invalidated too.
	public async Task<(int Orders, int BulkFiles)> RelabelPackageOnOrdersAsync(
		int packageId,
		string packageName,
		CancellationToken cancellationToken)
	{
		var result = await _atsRepository.RelabelPackageOnOrdersAsync(packageId, packageName, cancellationToken);

		if (result.Orders > 0 || result.BulkFiles > 0)
		{
			await _hybridCache.RemoveByTagAsync(CacheTags.Report, cancellationToken);
			await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication, cancellationToken);
		}

		return result;
	}
}
