namespace FrontendWebassembly.DTO.ATS;

public class DisputeOrderRequestDTO
{
	public Guid EmailInvitationId { get; set; }

	/// <summary>
	/// The free text typed into "Please specify", required for all three categories. Shown as the
	/// acknowledgement email's details line; the server does not persist it.
	/// </summary>
	public string? DisputeReason { get; set; }

	/// <summary>
	/// The selected category label on its own - Billing, Report or Others - sent alongside
	/// <see cref="DisputeReason"/>. This is the value the server persists and the dispute list shows.
	/// Must stay in step with the server's <c>ATS.DTO.DisputeOrderRequestDTO</c> - the two are bound
	/// by JSON property name and nothing checks that they agree.
	/// </summary>
	public string? DisputeCategory { get; set; }
}
