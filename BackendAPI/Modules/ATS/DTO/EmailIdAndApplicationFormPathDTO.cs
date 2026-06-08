namespace ATS.DTO;

public record EmailIdAndApplicationFormPathDTO
{
	public Guid EmailId { get; set; }
	public string? ApplicationFormPath { get; set; }
	public DateTime? ExpiresAt { get; set; }
	public string? Status { get; set; }

}
