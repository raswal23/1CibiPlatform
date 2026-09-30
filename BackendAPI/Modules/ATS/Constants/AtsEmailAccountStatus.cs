namespace ATS.Constants;

/// <summary>
/// The verification states a sender account moves through. Stored as the string name on
/// <see cref="ATS.Data.Entities.AtsEmailAccount.VerificationStatus"/>.
/// </summary>
/// <remarks>
/// Strings rather than an enum for the same reason as <see cref="AtsNotificationType"/>: the
/// value is persisted, so it must stay readable in the database and survive a reorder.
///
/// Only <see cref="Verified"/> is sendable. The selector checks this and nothing else infers
/// it, so an account that is mid-registration or whose password just stopped working cannot
/// reach the queue.
/// </remarks>
public static class AtsEmailAccountStatus
{
	/// <summary>
	/// Registered, credentials accepted by the provider, but the code has not been confirmed
	/// yet. Invisible to the selector, so a half-finished registration never sends.
	/// </summary>
	public const string Pending = "Pending";

	/// <summary>Code confirmed. The only status the selector will send through.</summary>
	public const string Verified = "Verified";

	/// <summary>
	/// Was verified, then the provider rejected its credentials (an auth-shaped permanent
	/// failure, typically a revoked or rotated app password). Removed from rotation until
	/// someone re-enters the password and passes the code again.
	/// </summary>
	/// <remarks>
	/// Distinct from cooling down, which is temporary and clears itself. This one never clears
	/// on its own, because nothing about waiting fixes a password the provider has revoked.
	/// </remarks>
	public const string NeedsReverification = "NeedsReverification";
}
