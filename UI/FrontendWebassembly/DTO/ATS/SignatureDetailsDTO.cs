namespace FrontendWebassembly.DTO.ATS;

public record SignatureDetailsDTO
{
	public Guid EmailInvitationID { get; set; }
	public byte[]? Signature { get; set; }
	public string? SignatureFileName { get; set; }
	public string? SignerName { get; set; }
	public DateOnly SignatureDate { get; set; }
}
