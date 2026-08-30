namespace ATS.Services.OrderValidation;

/// <summary>
/// Shared by the web console, the public API and the bulk CSV parser so all three agree
/// on what a valid order is. Before this existed each path checked only string length,
/// so any text was accepted as a package or an order type and the mistake only surfaced
/// later — at OMS ticketing, where an unmatched package parks the order with an opaque
/// reason.
/// </summary>
public sealed class OrderInputValidator : IOrderInputValidator
{
	// A client's assigned package list is short; this is a ceiling, not a page size.
	private const int MaxAssignedPackages = 200;

	private readonly IPackageManagementService _packageManagementService;
	private readonly ICurrentUser _currentUser;

	public OrderInputValidator(
		IPackageManagementService packageManagementService,
		ICurrentUser currentUser)
	{
		_packageManagementService = packageManagementService;
		_currentUser = currentUser;
	}

	public async Task<ValidatedOrderInput> ValidateAsync(
		string? package,
		string? orderType,
		CancellationToken cancellationToken)
	{
		// Order type first: it needs no database round trip, so an obviously wrong
		// request fails without one.
		if (OrderType.Normalize(orderType) is not { } normalizedOrderType)
		{
			throw new BadRequestException(
				$"'{orderType}' is not a valid order type. Use one of: {string.Join(", ", OrderType.All)}.");
		}

		if (string.IsNullOrWhiteSpace(package))
		{
			throw new BadRequestException("Package is required.");
		}

		var assignedPackages = await GetAssignedPackagesAsync(cancellationToken);

		if (assignedPackages.Count == 0)
		{
			throw new BadRequestException(
				"No screening package is assigned to this client, so an order cannot be created.");
		}

		var matched = assignedPackages.FirstOrDefault(assigned => string.Equals(
			assigned.PackageName,
			package.Trim(),
			StringComparison.OrdinalIgnoreCase));

		if (matched is null)
		{
			// Naming the client's own packages turns a rejection into something the
			// caller can act on. It discloses nothing they could not already read from
			// GET /packages.
			var available = string.Join(", ", assignedPackages.Select(assigned => assigned.PackageName));

			throw new BadRequestException(
				$"'{package}' is not a package available to this client. Use one of: {available}.");
		}

		// The id is what the order stores; the stored spelling of the name travels with
		// it as a label, so a caller who sent "criminal records check" is echoed the
		// canonical form.
		return new ValidatedOrderInput(matched.PackageId, matched.PackageName, normalizedOrderType);
	}

	public async Task<IReadOnlyList<PackageDetailsDTO>> GetAssignedPackagesAsync(
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			Cursor: null,
			PageSize: MaxAssignedPackages);

		// The client comes from the caller's token, never from the request, so one
		// client cannot order against another's entitlements.
		var packages = await _packageManagementService.GetPackagesAsync(
			paginationRequest,
			cancellationToken,
			_currentUser.AtsClientId);

		return packages.Items
			.Where(assigned => assigned.IsActive)
			.DistinctBy(assigned => assigned.PackageId)
			.OrderBy(assigned => assigned.PackageName)
			.ToArray();
	}
}
