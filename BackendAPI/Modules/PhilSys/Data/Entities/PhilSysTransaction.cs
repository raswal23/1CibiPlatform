namespace PhilSys.Data.Entities;

public class PhilSysTransaction
{
	public Guid Tid { get; set; }
	public string? InquiryType { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? Suffix { get; set; }
	public string? BirthDate { get; set; }
	public string? PCN { get; set; }
	public string? FaceLivenessSessionId { get; set; }
	public string? WebHookUrl { get; set; }
	public bool IsTransacted { get; set; }
	public string? HashToken { get; set; }
	public DateTime? UpdatedLivenessIdAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? TransactedAt { get; set; }
}
