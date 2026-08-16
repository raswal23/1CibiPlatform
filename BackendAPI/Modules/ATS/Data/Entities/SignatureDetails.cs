
namespace ATS.Data.Entities;

public class SignatureDetails
{
	public Guid SignatureDetailsID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? ConsentFormFileKey { get; set; }
	public string? ConsentFormFileName { get; set; }
	public DateTime? ConsentGeneratedAt { get; set; }
	public string? SignerName { get; set; }
	public DateOnly? SignatureDate { get; set; }

}
