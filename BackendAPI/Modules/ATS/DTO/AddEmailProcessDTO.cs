namespace ATS.DTO;

/// <summary>
/// Registers the copy list for a notice that does not have one yet.
/// </summary>
/// <remarks>
/// No <c>CreatedDate</c>: the server stamps it. A caller-supplied creation date is a caller
/// -supplied lie waiting to happen, and nothing reads it but the screen.
/// </remarks>
public class AddEmailProcessDTO
{
	/// <summary>
	/// Which notice this list belongs to - one of <see cref="ATS.Constants.AtsEmailProcess.All"/>.
	/// </summary>
	/// <remarks>
	/// Free text on the wire but closed in the validator: the send path looks a process up by
	/// this exact string, so a row registered against "Withdrawal" would be a copy list no
	/// notice ever reads, and nothing downstream would report it as a mistake.
	/// </remarks>
	public string EmailProcess { get; set; } = string.Empty;

	/// <summary>
	/// The mailboxes to copy, comma-separated. Stored normalised - trimmed, blanks dropped.
	/// </summary>
	public string CCEmail { get; set; } = string.Empty;

	public bool IsActive { get; set; }
}
