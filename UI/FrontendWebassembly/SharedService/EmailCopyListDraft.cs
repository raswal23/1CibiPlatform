namespace FrontendWebassembly.SharedService;

/// <summary>
/// The list-shaped rules for the comma-separated copy list on
/// <see cref="DTO.ATS.EmailProcessDetailsDTO.CCEmail"/> - splitting, joining, de-duplicating and
/// the two length caps. Mirrors <c>ATS.Shared.EmailCopyList</c>.
/// </summary>
/// <remarks>
/// Mirrored rather than shared because the WebAssembly project cannot reference the ATS module,
/// and the server stays the authority: every rule here is re-checked by
/// <c>EmailCopyList.Validate</c> behind the command validators, so a disagreement costs a 400 the
/// operator can read rather than a bad row. What this buys is that the operator hears it while
/// typing instead of after pressing Save.
///
/// Deliberately NOT here: whether an address is well formed. That is
/// <see cref="EmailValidationService.ValidateEmail"/>, which is already the UI's one definition
/// of an email address - the sender-account dialogs use it too, and a second regex in this file
/// is exactly the drift the backend's copy of that rule warns against.
///
/// The constants are duplicated on purpose and must stay equal to the backend's. They are the
/// column width and the per-address cap, so if one side moves the other has to move with it.
/// </remarks>
public static class EmailCopyListDraft
{
	/// <summary>A plain comma. An address cannot contain one, so the delimiter needs no escaping.</summary>
	public const char Delimiter = ',';

	/// <summary>Matches the <c>varchar(1000)</c> on <c>EmailProcessDetails.CCEmail</c>.</summary>
	public const int MaxLength = 1000;

	/// <summary>Matches the backend's per-address cap, which the column itself cannot express.</summary>
	public const int MaxAddressLength = 255;

	/// <summary>The addresses in a list, trimmed, with blanks dropped.</summary>
	public static IReadOnlyList<string> Split(string? copyList)
	{
		if (string.IsNullOrWhiteSpace(copyList))
		{
			return [];
		}

		return copyList.Split(
			Delimiter,
			StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
	}

	/// <summary>
	/// The canonical form: trimmed entries, blanks dropped, joined with a bare comma. Empty or
	/// whitespace-only input becomes the empty string, which is how "nobody is copied" is stored.
	/// </summary>
	public static string Normalize(string? copyList) => string.Join(Delimiter, Split(copyList));

	/// <summary>
	/// Whether an address is already in the list, matched case-insensitively.
	/// </summary>
	/// <remarks>
	/// Case-insensitive because "A@x.com" and "a@x.com" are one mailbox to every provider. Keeping
	/// both would copy that person twice and charge the sending account twice against its daily
	/// cap, and the unique index cannot see it - the whole list is one distinct string.
	/// </remarks>
	public static bool Contains(IEnumerable<string> addresses, string candidate) =>
		addresses.Any(address => string.Equals(address, candidate, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// The length of the list as it will be STORED, which is what the 1000-character cap is
	/// measured against.
	/// </summary>
	/// <remarks>
	/// Measured on the joined form rather than on what the operator typed, because the spacing
	/// around a comma is dropped on write. A list that is only over the limit because of its
	/// spaces is accepted by the server, so rejecting it here would be stricter than the thing it
	/// is trying to predict.
	/// </remarks>
	public static int NormalizedLength(IEnumerable<string> addresses) =>
		string.Join(Delimiter, addresses).Length;
}
