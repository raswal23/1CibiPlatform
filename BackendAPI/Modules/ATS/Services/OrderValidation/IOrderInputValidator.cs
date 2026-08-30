namespace ATS.Services.OrderValidation;

/// <summary>
/// The package and order type an order was placed with, canonicalised.
/// </summary>
public record ValidatedOrderInput(string Package, string OrderType);

public interface IOrderInputValidator
{
	/// <summary>
	/// Checks that the order type is Rush or Normal and that the package exists, is
	/// active, and is assigned to the caller's client — then returns both in their
	/// canonical spelling.
	///
	/// Throws <see cref="BadRequestException"/> naming the acceptable values, so a
	/// caller can correct the request without guessing. The message lists the client's
	/// own packages rather than every package on the platform.
	/// </summary>
	Task<ValidatedOrderInput> ValidateAsync(
		string? package,
		string? orderType,
		CancellationToken cancellationToken);

	/// <summary>
	/// The active packages assigned to the caller's client.
	/// </summary>
	Task<IReadOnlyList<PackageDetailsDTO>> GetAssignedPackagesAsync(CancellationToken cancellationToken);
}
