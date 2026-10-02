namespace ATS.DTO;

/// <summary>
/// What the server knows about an invitation after resolving it from a hash token.
/// The anonymous application-form endpoints authorize against this, never against an
/// EmailInvitationID supplied in the request body.
/// </summary>
public record ApplicationFormClaimDTO
{
	public Guid EmailInvitationID { get; init; }

	// The link itself no longer expires, so the form's own status is the whole
	// authorization decision: only a Pending form may be opened or written to.
	public string? ApplicationFormStatus { get; init; }
}
