namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// Edits an existing copy list, mirroring <c>ATS.DTO.EditEmailProcessDTO</c>.
/// </summary>
/// <remarks>
/// Carries the name because the dialog lets an operator retitle a row - but changing it moves the
/// list onto a different notice, since the send path matches on that string. The backend rejects a
/// name another row already holds, and rejects anything outside
/// <see cref="ShareData.ATS.AtsEmailProcesses.All"/> before that.
/// </remarks>
public class EditEmailProcessDTO
{
	public int Id { get; set; }

	public string EmailProcess { get; set; } = string.Empty;

	/// <summary>
	/// The mailboxes to copy, comma-separated. Replaces the stored list wholesale.
	/// </summary>
	public string CCEmail { get; set; } = string.Empty;

	public bool IsActive { get; set; }
}
