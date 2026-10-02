namespace FrontendWebassembly.ShareData.ATS;

/// <summary>
/// The ATS notices a copy list can be registered against, mirroring
/// <c>ATS.Constants.AtsEmailProcess</c>.
/// </summary>
/// <remarks>
/// The <see cref="All"/> values are what goes on the wire and what the send path matches on, so
/// they are spelled exactly as stored - no spaces, no hyphens, no re-casing. <see cref="Label"/>
/// is the only place the readable form exists, and it is display-only: nothing compares against
/// it. Keeping the two apart is what stops a wording change on this screen from silently
/// unregistering a copy list, because the backend validator closes the submitted value to the
/// same literals and would reject anything a label drifted into.
///
/// The order matches the backend's, which is also the order the table lists them in.
/// </remarks>
public static class AtsEmailProcesses
{
	/// <summary>Tells the requestor to stop the verification on a withdrawn order.</summary>
	public const string Withdrawn = "Withdrawn";

	/// <summary>The order-dispute notice.</summary>
	public const string Dispute = "Dispute";

	/// <summary>The first application-form invitation sent to a candidate.</summary>
	public const string ApplicationForm = "ApplicationForm";

	/// <summary>The reminder chasing a candidate who has not completed the form yet.</summary>
	public const string FollowUp = "FollowUp";

	/// <summary>Tells the requestor a candidate has completed their form.</summary>
	public const string SubmittedForm = "SubmittedForm";

	public static readonly string[] All =
	[
		Withdrawn,
		Dispute,
		ApplicationForm,
		FollowUp,
		SubmittedForm
	];

	/// <summary>
	/// The readable name for a process value. An unknown value comes back as it arrived rather
	/// than blank: a row hand-inserted against a name this list does not have is still a row the
	/// operator has to be able to see and fix, and an empty cell would hide it.
	/// </summary>
	public static string Label(string emailProcess) => emailProcess switch
	{
		Withdrawn => "Withdrawn order",
		Dispute => "Dispute",
		ApplicationForm => "Application form",
		FollowUp => "Follow-up reminder",
		SubmittedForm => "Submitted form",
		_ => emailProcess
	};
}
