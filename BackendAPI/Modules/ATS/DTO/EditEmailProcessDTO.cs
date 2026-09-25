namespace ATS.DTO;

/// <summary>
/// Edits an existing copy list.
/// </summary>
/// <remarks>
/// Carries no <c>EmailProcess</c>, and that is the point of the shape: a row IS the copy list
/// for its notice, matched by name on the send path. Letting an edit retype the process would
/// move a list from one notice to another with no trace that it happened, and would need the
/// unique-index collision handled for what nobody actually wants to do. To change which notice
/// a list serves, deactivate the row and add the other one.
/// </remarks>
public class EditEmailProcessDTO
{
	public int Id { get; set; }

	/// <summary>
	/// The mailboxes to copy, comma-separated. Replaces the stored list wholesale - the screen
	/// edits the whole list as one value, so a partial send would be indistinguishable from a
	/// deliberate removal.
	/// </summary>
	public string CCEmail { get; set; } = string.Empty;

	public bool IsActive { get; set; }
}
