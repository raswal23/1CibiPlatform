namespace ATS.Shared;

/// <summary>
/// Reads, normalises and checks the comma-separated copy list held in
/// <see cref="ATS.Data.Entities.EmailProcessDetails.CCEmail"/>.
/// </summary>
/// <remarks>
/// That column stores a LIST inside one string, which the database cannot police: a duplicated
/// address, a malformed mailbox and stray whitespace are all a perfectly valid
/// <c>varchar(1000)</c>. See the remarks on the entity. This class is the only place that CAN
/// police it, so the command validators, the service that normalises before writing, and the
/// send path that splits before handing addresses to <c>MimeKit.MailboxAddress.Parse</c> all go
/// through here rather than each calling <c>string.Split</c> with its own idea of the rules.
///
/// The per-address shape check delegates to <see cref="BulkSubjectRowValidator.IsValidEmail"/>
/// rather than restating one. That method is already the module's shared definition of a valid
/// address - the bulk parser and the upload-time check both call it "so the two tiers cannot
/// drift" - and a fourth definition here would be a fourth thing to keep in step.
/// </remarks>
public static class EmailCopyList
{
	/// <summary>
	/// A plain comma, no spaces. A comma cannot appear inside an email address, so the
	/// delimiter needs no escaping.
	/// </summary>
	public const char Delimiter = ',';

	/// <summary>Matches the <c>varchar(1000)</c> on <c>EmailProcessDetails.CCEmail</c>.</summary>
	public const int MaxLength = 1000;

	/// <summary>
	/// Matches <c>AtsEmailAccount.EmailAddress</c>, the sibling column that holds one address.
	/// The list column cannot constrain a single entry, so the bound is applied here instead.
	/// </summary>
	public const int MaxAddressLength = 255;

	/// <summary>
	/// The addresses in a stored list, trimmed, with blanks dropped.
	/// </summary>
	/// <remarks>
	/// Trims even though <see cref="Normalize"/> writes the list without spaces: the column is
	/// hand-editable, and a row written before this class existed - or edited straight in the
	/// database - can carry "a@x.com, b@x.com". A leading space makes the fragment unparseable
	/// to MimeKit, and it throws for the whole notice rather than for the one bad address.
	/// </remarks>
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
	/// The canonical form written to the column: trimmed entries, blanks dropped, joined with
	/// a bare comma. An empty or whitespace-only input becomes the empty string, which is how
	/// "nobody is copied on this notice" is stored.
	/// </summary>
	public static string Normalize(string? copyList) =>
		string.Join(Delimiter, Split(copyList));

	/// <summary>
	/// The first problem with a submitted list, or <see langword="null"/> when it is fit to
	/// store. Returns a message rather than throwing, so a validator can surface it as a 400
	/// against the property the caller sent.
	/// </summary>
	/// <remarks>
	/// The empty list passes. Whether an empty list may also be ACTIVE is the command's rule,
	/// not this one's: an active row with no addresses is what would hand "" to MimeKit, and
	/// the two commands each state that rule against their own <c>IsActive</c>.
	/// </remarks>
	public static string? Validate(string? copyList)
	{
		var addresses = Split(copyList);

		if (addresses.Count == 0)
		{
			return null;
		}

		// Case-insensitive: "A@x.com" and "a@x.com" are one mailbox to every provider, so
		// keeping both would copy that person twice and charge the sending account twice
		// against its daily cap.
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var address in addresses)
		{
			// Checked before the shape rule, and the offending value is deliberately not
			// echoed - a 900-character entry would otherwise be repeated into the response.
			if (address.Length > MaxAddressLength)
			{
				return $"An email address in the copy list cannot exceed {MaxAddressLength} characters.";
			}

			if (!BulkSubjectRowValidator.IsValidEmail(address))
			{
				return $"'{address}' is not a valid email address.";
			}

			if (!seen.Add(address))
			{
				return $"'{address}' appears more than once in the copy list.";
			}
		}

		// Measured on the NORMALISED string, because that is what gets stored. A list that is
		// only over the limit because of the spaces around its commas is accepted, and the
		// spaces are dropped on write.
		if (string.Join(Delimiter, addresses).Length > MaxLength)
		{
			return $"The copy list cannot exceed {MaxLength} characters.";
		}

		return null;
	}
}
