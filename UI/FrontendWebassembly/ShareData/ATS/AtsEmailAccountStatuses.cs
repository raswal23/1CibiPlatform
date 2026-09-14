namespace FrontendWebassembly.ShareData.ATS;

/// <summary>
/// Sender-account verification states, mirroring <c>ATS.Constants.AtsEmailAccountStatus</c>.
/// The value arrives as a string on the DTO; these are what the table matches on.
/// </summary>
/// <remarks>
/// Only <see cref="Verified"/> is sendable, and the backend is the one that decides that - the
/// table reads <c>IsSendable</c> for the badge rather than re-deriving the rule here, so the two
/// cannot disagree. These names exist for the wording of the status pill, not for the logic.
/// </remarks>
public static class AtsEmailAccountStatuses
{
	public const string Pending = "Pending";
	public const string Verified = "Verified";
	public const string NeedsReverification = "NeedsReverification";
}
