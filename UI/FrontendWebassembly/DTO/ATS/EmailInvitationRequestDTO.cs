namespace FrontendWebassembly.DTO.ATS;

public record EmailInvitationRequestDTO
{
	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? EmailAddress { get; set; }
	public string? MobileNumber { get; set; }
	public string? SelectPackage { get; set; }
	public string? RushNormal { get; set; }

	// Screening type (true = manual, false = data); gates the package list and
	// whether the identity fields below are required.
	public bool? AutoChasing { get; set; }

	// Candidate identity, required only for data screening (no application form
	// is sent, so the candidate cannot supply them later).
	public DateOnly? DateOfBirth { get; set; }
	public string? SSSNumber { get; set; }
	public string? TINNumber { get; set; }
}
