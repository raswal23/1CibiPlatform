namespace ATS.DTO;

/// <summary>
/// One notice's copy list as the management screen reads it back.
/// </summary>
public class EmailProcessDetailsDTO
{
	public int Id { get; set; }

	/// <summary>One of <see cref="ATS.Constants.AtsEmailProcess.All"/>.</summary>
	public string EmailProcess { get; set; } = string.Empty;

	/// <summary>
	/// Every copied mailbox, comma-separated - the column verbatim, already normalised on
	/// write. Empty means nobody is copied.
	/// </summary>
	public string CCEmail { get; set; } = string.Empty;

	public DateTime CreatedDate { get; set; }

	public bool IsActive { get; set; }
}
