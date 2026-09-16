namespace FrontendWebassembly.DTO.ATS;

public record EmailIdAndApplicationFormPathDTO
{
	public Guid EmailId { get; set; }
	public string Status { get; set; }

	// Birth date captured at order entry, when known; pre-fills the form.
	public DateOnly? DateOfBirth { get; set; }
}
