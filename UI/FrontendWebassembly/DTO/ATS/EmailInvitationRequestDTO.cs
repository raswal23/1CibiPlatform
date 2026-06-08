namespace FrontendWebassembly.DTO.ATS;

public record EmailInvitationRequestDTO
{
	public Guid EmailInvitationID { get; set; }
	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? EmailAddress { get; set; }
	public string? MobileNumber { get; set; }
	public string? SelectPackage { get; set; }
	public string? RushNormal { get; set; }
	public string? HashToken { get; set; }
	public string? Status { get; set; }
	public DateTime? HashTokenCreated { get; set; }
	public DateTime? HashTokenExpiration { get; set; }

}
