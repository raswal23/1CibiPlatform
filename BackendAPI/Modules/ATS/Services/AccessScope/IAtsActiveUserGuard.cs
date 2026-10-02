namespace ATS.Services.AccessScope;

public interface IAtsActiveUserGuard
{
	/// <summary>
	/// Throws <see cref="ForbiddenException"/> (403) when the current caller has no
	/// active ATS account - no <c>ats.UserDetails</c> row with <c>IsActive</c> and an
	/// active role. Platform super admins pass without a row: they administer ATS
	/// without being ATS users themselves.
	/// </summary>
	Task EnsureActiveAtsUserAsync(CancellationToken cancellationToken);
}
