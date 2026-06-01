
namespace ATS.Data.Entities;

public class SignatureDetails
{
	public Guid SignatureDetailsID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? SignatureFileKey { get; set; }
	public string? Signature { get; set; }
	public string? SignerName { get; set; }
	public DateOnly? SignatureDate { get; set; } 

}
