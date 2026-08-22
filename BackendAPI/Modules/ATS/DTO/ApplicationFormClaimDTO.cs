namespace ATS.DTO;

/// <summary>
/// What the server knows about an invitation after resolving it from a hash token.
/// The anonymous application-form endpoints authorize against this, never against an
/// EmailInvitationID supplied in the request body.
/// </summary>
public record ApplicationFormClaimDTO
{
	public Guid EmailInvitationID { get; init; }

	public DateTime? HashTokenExpiration { get; init; }

	public string? ApplicationFormStatus { get; init; }

	public bool IsExpired => !HashTokenExpiration.HasValue
		|| HashTokenExpiration.Value <= DateTime.UtcNow;
}
