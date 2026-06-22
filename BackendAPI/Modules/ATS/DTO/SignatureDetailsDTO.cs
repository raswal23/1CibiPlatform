namespace ATS.DTO;

public class SignatureDetailsDTO
{
	public Guid EmailInvitationID { get; set; }
	public IFormFile? Signature { get; set; }
	public string? SignatureFileName { get; set; }
	public string? SignerName { get; set; }
	public DateOnly SignatureDate { get; set; }
}
