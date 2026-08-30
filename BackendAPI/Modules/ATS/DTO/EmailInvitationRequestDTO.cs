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
	public string? SelectPackage { get; set; }
	public string? RushNormal { get; set; }
}
