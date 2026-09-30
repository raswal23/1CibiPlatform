namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// Registers the copy list for a notice that does not have one yet, mirroring
/// <c>ATS.DTO.AddEmailProcessDTO</c>.
/// </summary>
/// <remarks>
/// No creation date - the server stamps it. The name is picked from
/// <see cref="ShareData.ATS.AtsEmailProcesses.All"/> rather than typed freely: the send path
/// looks a row up by that exact string, and the backend validator closes it to the same list, so
/// anything else would be rejected there anyway.
/// </remarks>
public class AddEmailProcessDTO
{
	public string EmailProcess { get; set; } = string.Empty;

	/// <summary>The mailboxes to copy, comma-separated. Normalised again on the server.</summary>
	public string CCEmail { get; set; } = string.Empty;

	public bool IsActive { get; set; }
}
