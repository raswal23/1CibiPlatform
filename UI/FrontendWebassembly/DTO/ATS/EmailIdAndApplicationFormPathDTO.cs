namespace FrontendWebassembly.DTO.ATS;

public record EmailIdAndApplicationFormPathDTO
{
	public Guid EmailId { get; set; }
	public string? ApplicationFormPath { get; set; }
	public DateTime? ExpiresAt { get; set; }
	public bool IsExpired { get; set;  }
	public string? Status { get; set; }
}
