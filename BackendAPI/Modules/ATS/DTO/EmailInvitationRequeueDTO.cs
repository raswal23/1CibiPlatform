namespace ATS.DTO;

/// <summary>
/// One invitation to put back on the email queue, with the token it should carry.
///
/// The token travels with the id because every requeued invitation needs its OWN freshly
/// generated one - reusing a single token across a batch would let any candidate in it open
/// another candidate's application form. Generating them is the service's job (it owns
/// <c>ISecureToken</c> and <c>IHashService</c>), so the repository is handed the finished
/// values rather than reaching for those itself.
/// </summary>
public sealed class EmailInvitationRequeueDTO
{
	public Guid EmailInvitationId { get; set; }

	public string HashToken { get; set; } = string.Empty;

	public DateTime HashTokenExpiration { get; set; }
}
