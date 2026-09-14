namespace ATS.DTO;

public record EmailIdAndApplicationFormPathDTO
{
	public Guid EmailId { get; set; }
	public string? ApplicationFormPath { get; set; }
	public DateTime? ExpiresAt { get; set; }
	public string? Status { get; set; }

	// Captured at order entry when the requestor already knows it (data-screening
	// orders); the form pre-fills the birth date so the candidate need not retype it.
	public DateOnly? DateOfBirth { get; set; }
}
