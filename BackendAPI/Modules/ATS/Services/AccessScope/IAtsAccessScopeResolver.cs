namespace ATS.Services.AccessScope;

/// <summary>
/// The set of records an ATS caller may read.
/// </summary>
/// <param name="AuthorizedClientIds">
/// null means every client (platform super admin). An empty collection means no client,
/// which filters everything out - empty is not the same as null.
/// </param>
/// <param name="RequiredOwnerId">
/// When set, the caller may only see records they personally created.
/// </param>
public readonly record struct AtsAccessScope(
	IReadOnlyCollection<int>? AuthorizedClientIds,
	Guid? RequiredOwnerId);

public interface IAtsAccessScopeResolver
{
	/// <summary>
	/// Returns null when the caller may not read ATS records at all. A non-null value
	/// carries the client/owner predicates the query must apply.
	/// </summary>
	Task<AtsAccessScope?> ResolveAsync(CancellationToken cancellationToken);
}
