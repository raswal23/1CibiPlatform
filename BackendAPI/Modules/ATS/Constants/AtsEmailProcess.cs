namespace ATS.Constants;

/// <summary>
/// The ATS candidate-facing notices a copy list can be registered against. Stored as the string
/// name on <see cref="ATS.Data.Entities.EmailProcessDetails.EmailProcess"/>.
/// </summary>
/// <remarks>
/// Strings rather than an enum for the same reason as <see cref="AtsNotificationType"/> and
/// <see cref="AtsEmailAccountStatus"/>: the value is persisted, so it has to stay readable in the
/// database and survive someone reordering the list.
///
/// Each value names one notice that today copies a hardcoded list. The row-backed table replaces
/// those literals, so the values here deliberately mirror the existing constants classes rather
/// than inventing a new vocabulary - see <see cref="WithdrawnEmail"/>, <see cref="DisputeEmail"/>
/// and <see cref="ApplicationFormEmail"/>.
///
/// <see cref="ApplicationForm"/> and <see cref="FollowUp"/> are separate values even though
/// <c>ApplicationFormEmail.CopyTeams</c> currently serves both. Splitting them is the point of
/// storing a process per row: the first contact and the chasing reminder go to the same list only
/// because a single literal could not distinguish them. Seeding both with the same addresses keeps
/// today's behaviour while making the two independently editable.
/// </remarks>
public static class AtsEmailProcess
{
	/// <summary>The withdrawn-order notice. See <see cref="WithdrawnEmail"/>.</summary>
	public const string Withdrawn = "Withdrawn";

	/// <summary>The order-dispute notice. See <see cref="DisputeEmail"/>.</summary>
	public const string Dispute = "Dispute";

	/// <summary>
	/// The first application form invitation sent to a candidate.
	/// </summary>
	public const string ApplicationForm = "ApplicationForm";

	/// <summary>
	/// The reminder chasing a candidate who has not completed the form yet.
	/// </summary>
	public const string FollowUp = "FollowUp";

	/// <summary>
	/// Every process a copy list can be registered against, in the order the management screen
	/// lists them. Follows <see cref="OrderType"/> and <see cref="TicketStatus"/>, which expose
	/// the same array so a validator and a dropdown share one vocabulary rather than each
	/// repeating the literals.
	/// </summary>
	public static readonly string[] All =
	[
		Withdrawn,
		Dispute,
		ApplicationForm,
		FollowUp
	];
}
