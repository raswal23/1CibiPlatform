namespace ATS.DTO;

// Intermediate projection for the reports lists. Rank materializes the status
// precedence used by the default sort so ORDER BY and the keyset seek predicate
// are guaranteed to use the identical SQL expression. Pages are fetched in this
// shape (the final ReportListDTO concatenates the name, losing the cursor sort
// keys) and mapped in the service.
public sealed class ReportRowDTO
{
	public Guid EmailInvitationID { get; init; }
	public string? FirstName { get; init; }
	public string? LastName { get; init; }
	public string? Requestor { get; init; }
	public string? OrderStatus { get; init; }
	public DateTime? OrderCompletedAt { get; init; }
	public string? SelectPackage { get; init; }
	public string? HitStatus { get; init; }
	public int Rank { get; init; }
}
