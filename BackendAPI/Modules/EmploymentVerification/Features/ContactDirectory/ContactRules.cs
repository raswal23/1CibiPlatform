namespace EmploymentVerification.Features.ContactDirectory;

/// <summary>
/// Validation rules shared by the add and edit contact slices. Two copies of the
/// cursor-delimiter rule would be the thing that drifts, and the reason it exists is
/// subtle enough that it needs explaining once rather than twice.
/// </summary>
internal static class ContactRules
{
	internal const string CursorDelimiterMessage =
		"Company name cannot contain the '|' character.";

	/// <summary>
	/// The keyset cursor is base64("company|id") and <c>CursorCodec.Decode</c> splits
	/// on '|', returning null when the field count is wrong. A company name carrying
	/// one would make every cursor minted from that row decode as null, silently
	/// resetting the user to the first page. Rejected at the edge rather than by
	/// escaping inside the shared codec, which every other keyset screen depends on.
	/// </summary>
	internal static bool HasNoCursorDelimiter(string? companyName) =>
		companyName is null || !companyName.Contains('|');
}
