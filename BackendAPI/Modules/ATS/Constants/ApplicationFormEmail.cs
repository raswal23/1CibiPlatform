namespace ATS.Constants;

/// <summary>
/// The CIBI mailboxes copied on the candidate-facing application form emails - the first invitation
/// and the package follow-up reminder.
/// </summary>
/// <remarks>
/// Only the copy list lives here, unlike <see cref="SubmittedFormEmail"/>, <see cref="WithdrawnEmail"/>
/// and <see cref="DisputeEmail"/>, which also hold their subject. Each of those is ONE notice with one
/// subject, interpolated into the body's header so the two sites cannot disagree in a mail client's
/// preview. These are TWO bodies with two subjects, already held as
/// <c>EndorsementSubmissionService.InvitationSubject</c> and <c>ReminderSubject</c> beside the send that
/// chooses between them - moving a pair of subjects here would separate them from the only code that
/// picks between them, and buy nothing.
///
/// Known and NOT fixed here: both bodies hardcode their header as a duplicate string literal rather
/// than interpolating those consts - see the <c>&lt;h1&gt;</c> in
/// <c>ATSEmailService.SendAppplicationFormNotification</c> and
/// <c>BuildApplicationFormReminderNotification</c>. The four strings currently agree. Unifying them is
/// a separate change to the bodies, out of scope for a copy list; until then, editing either subject
/// means editing its header too.
///
/// These are Cc rather than Bcc, matching the completed-form notice. The candidate therefore sees the
/// team addresses in their inbox, which is the intent - the closing sentence of both bodies already
/// tells them to write to <c>ccteam@cibi.com.ph</c>, so a visible copy agrees with the copy they read.
///
/// Every copied address is a real recipient to the provider and is charged to the sending account's
/// daily cap along with the candidate - see <c>ATSEmailService.SendThroughAccountAsync</c>, which logs
/// <c>1 + cc.Count</c>. Both bodies are sent for EVERY order, including the bulk queue, so each entry
/// added here multiplies the cap consumed by a batch. A 500-row upload with two copies consumes 1,500
/// of the cap rather than 500 - 2,000 once the requestor below is resolved onto the list as well.
/// </remarks>
public static class ApplicationFormEmail
{
	/// <summary>
	/// The CIBI mailboxes copied on every application form invitation and reminder, so both teams see
	/// each one regardless of which requestor raised the order. The candidate is the TO address and is
	/// never repeated here.
	/// </summary>
	/// <remarks>
	/// Held as a collection rather than a single address, matching <see cref="SubmittedFormEmail"/>: the
	/// two sibling notices that copy one team use a <c>const string</c>, and this list copies the same
	/// two mailboxes the completed-form notice does.
	///
	/// The FIXED part of the copy list only. The requestor who raised the order is copied too, but they
	/// vary per order and their mailbox is not on the order - it is resolved from the Auth directory by
	/// <c>EndorsementSubmissionService.BuildCopyListAsync</c>, which appends it to a copy of this list.
	/// Read that method for what the message actually carries.
	/// </remarks>
	// The commented block below is a tester's mailboxes, swapped in while a branch is being
	// verified and swapped back before release. The live list is the one in effect.
	//public static readonly IReadOnlyCollection<string> CopyTeams =
	//[
	//	"clientsupport@cibi.com.ph",
	//	"pre-workteam@cibi.com.ph"
	//];
	public static readonly IReadOnlyCollection<string> CopyTeams =
	[
		"svaldemoro@cibi.com.ph",
		"angel.condensada11@gmail.com"
	];
}
