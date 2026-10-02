namespace ATS.DTO;

public class DisputeOrderRequestDTO
{
	public Guid EmailInvitationId { get; set; }

	/// <summary>
	/// The free text the filer typed into "Please specify". Required for every category - Billing,
	/// Report and Others alike. Rendered as the acknowledgement email's details line and NOT
	/// persisted; there is no column for it, and adding one was deliberately avoided.
	/// </summary>
	public string? DisputeReason { get; set; }

	/// <summary>
	/// The selected category label on its own: Billing, Report or Others. This is what gets
	/// persisted, into <c>EmailInvitationRequest.DisputeCategory</c>, and what the console's dispute
	/// list shows. Optional on the wire only so a client that predates per-category free text - which
	/// sent the label in <see cref="DisputeReason"/> - still writes something meaningful.
	/// </summary>
	public string? DisputeCategory { get; set; }
}
