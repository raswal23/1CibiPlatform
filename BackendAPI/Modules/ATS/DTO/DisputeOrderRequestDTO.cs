namespace ATS.DTO;

public class DisputeOrderRequestDTO
{
	public Guid EmailInvitationId { get; set; }

	/// <summary>
	/// The dispute text. For Billing and Report this is the category label itself - the console
	/// sends one value for both - and for "Others" it is the free text the filer typed. This is
	/// what gets persisted, into <c>EmailInvitationRequest.DisputeCategory</c>.
	/// </summary>
	public string? DisputeReason { get; set; }

	/// <summary>
	/// The selected category label on its own: Billing, Report or Others. Used only to render the
	/// acknowledgement email's two lines, so it can show the category AND, for "Others", the free
	/// text separately instead of one or the other. Not persisted - there is no column for it, and
	/// adding one was deliberately avoided.
	/// </summary>
	public string? DisputeCategory { get; set; }
}
