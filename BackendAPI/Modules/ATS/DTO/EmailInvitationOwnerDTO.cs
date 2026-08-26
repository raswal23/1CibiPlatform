namespace ATS.DTO;

/// <summary>
/// The scope-relevant identity of one order: which client it belongs to and who
/// raised it. Enough to run an <c>AtsAccessScope</c> check without loading the
/// whole invitation row.
/// </summary>
public sealed class EmailInvitationOwnerDTO
{
	public Guid EmailInvitationID { get; init; }

	public int? ClientId { get; init; }

	public Guid? RequestorId { get; init; }
}
