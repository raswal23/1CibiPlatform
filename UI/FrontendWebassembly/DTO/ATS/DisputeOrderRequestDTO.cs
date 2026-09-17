namespace FrontendWebassembly.DTO.ATS;

public class DisputeOrderRequestDTO
{
	public Guid EmailInvitationId { get; set; }

	/// <summary>
	/// The dispute text. For Billing and Report this is the category label itself, and for "Others"
	/// it is the free text typed into "Please specify". This is what the server persists.
	/// </summary>
	public string? DisputeReason { get; set; }

	/// <summary>
	/// The selected category label on its own, sent alongside <see cref="DisputeReason"/> so the
	/// acknowledgement email can show the category and the free text as two lines. Must stay in
	/// step with the server's <c>ATS.DTO.DisputeOrderRequestDTO</c> - the two are bound by JSON
	/// property name and nothing checks that they agree.
	/// </summary>
	public string? DisputeCategory { get; set; }
}
