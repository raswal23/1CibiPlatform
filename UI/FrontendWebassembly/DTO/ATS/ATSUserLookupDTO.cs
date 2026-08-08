namespace FrontendWebassembly.DTO.ATS;

public class ATSUserLookupDTO
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;
}
