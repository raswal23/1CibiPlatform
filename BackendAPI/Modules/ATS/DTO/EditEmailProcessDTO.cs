namespace ATS.DTO;

/// <summary>
/// Edits an existing copy list.
/// </summary>
/// <remarks>
/// Carries <c>EmailProcess</c> so the management screen can retitle a row, but the value stays
/// closed to <see cref="ATS.Constants.AtsEmailProcess.All"/> in the validator for the same reason
/// it is closed on add: the send path looks a row up by that exact string, so a name outside the
/// list is a copy list no notice ever reads while the screen still shows it as configured.
///
/// Renaming is therefore a MOVE, not a relabel - it points an existing list at a different
/// notice - and the service guards it against the unique index the same way an add is guarded,
/// excluding the row being edited so leaving the name alone is not a collision with itself.
/// The index is case-sensitive while the send path's lookup is not, so the guard matches
/// case-insensitively: without it, "withdrawn" and "Withdrawn" would be two rows to PostgreSQL
/// and one notice to the sender, and which list wins would come down to row order.
///
/// No <c>CreatedDate</c>: that is the row's own, not the caller's.
/// </remarks>
public class EditEmailProcessDTO
{
	public int Id { get; set; }

	/// <summary>
	/// Which notice this list belongs to - one of <see cref="ATS.Constants.AtsEmailProcess.All"/>.
	/// </summary>
	public string EmailProcess { get; set; } = string.Empty;

	/// <summary>
	/// The mailboxes to copy, comma-separated. Replaces the stored list wholesale - the screen
	/// edits the whole list as one value, so a partial send would be indistinguishable from a
	/// deliberate removal.
	/// </summary>
	public string CCEmail { get; set; } = string.Empty;

	public bool IsActive { get; set; }
}
