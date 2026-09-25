namespace ATS.Services.Settings.EmailProcessManagement;

/// <summary>
/// Registers and edits the copy list each ATS notice sends to, and answers the send paths asking
/// who is copied on one.
/// </summary>
/// <remarks>
/// Both sides of the same table, deliberately on one interface: the console writes the rows and the
/// notices read them, and there is no third consumer.
///
/// The two sides do NOT share a failure stance, which is the thing to keep straight when editing
/// here. The write methods throw - <c>BadRequestException</c>, <c>NotFoundException</c> - because an
/// operator is looking at a screen and can fix what is rejected. <see cref="GetCopyListAsync"/>
/// never throws, because it runs behind an order that already committed with nobody watching, and a
/// copy list is not what makes a notice worth sending. Both are right for their caller. Do not
/// "make them consistent".
/// </remarks>
public interface IEmailProcessManagementService
{
	Task<IReadOnlyList<EmailProcessDetailsDTO>> GetEmailProcessesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// The mailboxes copied on <paramref name="emailProcess"/>, or an empty list when the notice
	/// has no usable copy list. Never null, never throws.
	/// </summary>
	/// <remarks>
	/// One method for all five notices rather than a lookup each send path writes itself, because
	/// the interesting part is not the query - it is what happens when the query does not give a
	/// usable answer, and that must not be decided five different ways. No row for the process, the
	/// row switched off, the database unreachable: all three degrade to an empty list and the notice
	/// still goes out to its real recipient. The withdrawal notice exists to tell the requestor to
	/// stop the verification; the application form exists to give the candidate their link. Losing
	/// the team copy degrades those. Losing the notice breaks them.
	///
	/// What it will NOT do is paper over a bad address. <see cref="EmailCopyList.Split"/> drops
	/// blanks and trims, which covers hand-edited whitespace, but a genuinely malformed mailbox that
	/// got into the column stays in the list and will throw in <c>MimeKit.MailboxAddress.Parse</c>
	/// for the whole notice. Filtering it out here would hide a broken copy list indefinitely - the
	/// operator would never learn an address was wrong, and the team would silently stop being
	/// copied. The command validators are what keep that out of the column.
	/// </remarks>
	/// <param name="emailProcess">A value from <see cref="AtsEmailProcess"/>.</param>
	Task<IReadOnlyList<string>> GetCopyListAsync(
		string emailProcess,
		CancellationToken cancellationToken);

	Task<EmailProcessDetailsDTO> AddEmailProcessAsync(
		AddEmailProcessDTO emailProcessDTO,
		CancellationToken cancellationToken);

	Task<EmailProcessDetailsDTO> EditEmailProcessAsync(
		EditEmailProcessDTO emailProcessDTO,
		CancellationToken cancellationToken);
}
