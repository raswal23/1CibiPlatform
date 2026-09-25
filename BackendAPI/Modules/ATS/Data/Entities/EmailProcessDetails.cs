namespace ATS.Data.Entities;

/// <summary>
/// The copy list for one ATS notice - the row-backed replacement for the hardcoded copy lists.
/// </summary>
/// <remarks>
/// The copy lists used to be compile-time literals - see <see cref="ATS.Constants.ApplicationFormEmail"/>,
/// whose <c>CopyTeams</c> is still a hardcoded array that a deployment has to be rebuilt to change.
/// Rows let an operator add or retire a copied mailbox without a release, which is the whole reason
/// this table exists.
///
/// The grain is ONE ROW PER NOTICE, with every copied mailbox held in <see cref="CCEmail"/> as a
/// comma-separated list. The whole list for a notice is therefore read, edited and saved as a single
/// value, which is what the management screen edits.
///
/// The cost of that shape, recorded here because it cannot be fixed downstream: the database can
/// constrain the COLUMN but not its CONTENTS. A duplicated address inside one string, a malformed
/// mailbox, or stray whitespace are all valid as far as the unique index and the length limit are
/// concerned. Nothing rejects them on write, so the send path must split, trim and drop blanks
/// before handing anything to <c>MimeKit.MailboxAddress.Parse</c> - an unparseable fragment throws,
/// and it throws for the whole notice rather than for the one bad address. Validation belongs in the
/// command's <c>AbstractValidator</c>, which is the only place that CAN enforce it.
///
/// <see cref="IsActive"/> rather than deleting the row: a notice's copy list is usually suspended
/// temporarily, and a soft flag keeps the addresses and the <see cref="CreatedDate"/> so restoring it
/// is one toggle rather than re-entering the list.
///
/// Every address here is a real recipient to the provider and IS charged to the sending account's
/// daily cap alongside the candidate, while the send log records the TO address only - the same
/// arithmetic <c>ApplicationFormEmail</c> documents. Each address added to a list multiplies the cap
/// a batch really consumes, so the headroom in <c>DefaultDailySendLimit</c> is what absorbs it.
/// </remarks>
public sealed class EmailProcessDetails
{
	public int Id { get; set; }

	// AtsEmailProcess, stored as its string name rather than an int so a row stays readable in the
	// database and reordering the list cannot silently retype history - the same reasoning as
	// AtsNotification.Type and AtsEmailAccount.VerificationStatus.
	public string EmailProcess { get; set; } = string.Empty;

	// Every mailbox copied on this notice, comma-separated - for example
	// "clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph". Empty means nobody is copied.
	//
	// The delimiter is a plain comma with no spaces. A comma cannot appear inside an email address,
	// so it needs no escaping, and fixing the spacing means a reader can split on ',' without
	// trimming - though the send path trims anyway, because this column is hand-edited.
	public string CCEmail { get; set; } = string.Empty;

	public DateTime CreatedDate { get; set; }

	// Operator's switch. The send path copies this list only when the row is active; an inactive
	// row keeps its addresses on file so it can be turned back on without re-entry.
	public bool IsActive { get; set; }
}
