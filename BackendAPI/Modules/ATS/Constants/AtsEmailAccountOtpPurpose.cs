namespace ATS.Constants;

/// <summary>
/// What a pending sender-account code approves. Stored as the string name on
/// <see cref="ATS.Data.Entities.AtsEmailAccountOtp.Purpose"/>.
/// </summary>
/// <remarks>
/// Codes are scoped to one purpose so a code minted to confirm a password change cannot be
/// replayed against the delete endpoint. Verification matches on purpose as well as account,
/// which is what makes that guarantee real rather than a convention.
/// </remarks>
public static class AtsEmailAccountOtpPurpose
{
	/// <summary>Proves the credentials of a newly registered account.</summary>
	public const string Register = "Register";

	/// <summary>
	/// Approves a change to email address, password, host or port. Those four are the only
	/// fields that can invalidate the proof a previous code gave; priority, display name, daily
	/// limit and the active flag save without one.
	/// </summary>
	public const string Edit = "Edit";

	/// <summary>
	/// Approves removing the account, at the operator's request - deleting a sender is as
	/// consequential as adding one, since the remaining accounts absorb its volume.
	/// </summary>
	public const string Delete = "Delete";
}
