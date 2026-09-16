namespace ATS.DTO;

public record EmailInvitationRequestDTO
{
	// Set by InsertEmailInvitationRequestAsync once the order exists, so an API caller
	// can record and poll what they just created. The web console ignores it.
	public Guid OrderId { get; set; }

	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? EmailAddress { get; set; }
	public string? MobileNumber { get; set; }
	// Resolved from SelectPackage by the order validator, not supplied by the caller.
	public int PackageId { get; set; }

	public string? SelectPackage { get; set; }
	public string? RushNormal { get; set; }

	// Screening type (true = manual, false = data). The web console sends the chosen
	// value and the service cross-checks it against the selected package; public API
	// and assistant callers send null and skip the check. Before persisting, the
	// service overwrites it with the package's own classification, which is
	// snapshotted onto the order.
	public bool? AutoChasing { get; set; }

	// Candidate identity, required (by the web validator) only when AutoChasing is
	// false: a data screening sends no application form, so the candidate cannot
	// supply these later.
	public DateOnly? DateOfBirth { get; set; }
	public string? SSSNumber { get; set; }
	public string? TINNumber { get; set; }
}
