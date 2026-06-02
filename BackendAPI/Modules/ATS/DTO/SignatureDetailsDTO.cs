namespace ATS.DTO;

public class SignatureDetailsDTO
{
	public Guid SignatureDetailsID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public byte[]? Signature { get; set; }
	public string? SignatureFileName { get; set; }
	public string? SignerName { get; set; }
	public DateOnly SignatureDate { get; set; }
}
