namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// One ATS notice's copy list as the management table shows it, mirroring
/// <c>ATS.DTO.EmailProcessDetailsDTO</c>.
/// </summary>
/// <remarks>
/// <see cref="CCEmail"/> is the stored column verbatim - every mailbox in one comma-separated
/// string, already normalised on write. The table renders it as chips and the edit dialog binds
/// it through the chip input, but neither changes the wire shape: the backend replaces the whole
/// list, so a partial send would be indistinguishable from a deliberate removal.
/// </remarks>
public class EmailProcessDetailsDTO
{
	public int Id { get; set; }

	/// <summary>One of the values in <c>AtsEmailProcesses.All</c>.</summary>
	public string EmailProcess { get; set; } = string.Empty;

	public string CCEmail { get; set; } = string.Empty;

	public DateTime CreatedDate { get; set; }

	/// <summary>An inactive list copies nobody; the notice itself still goes to its recipient.</summary>
	public bool IsActive { get; set; }
}
