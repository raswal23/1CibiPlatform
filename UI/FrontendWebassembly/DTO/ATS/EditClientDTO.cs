namespace FrontendWebassembly.DTO.ATS;

public class EditClientDTO
{
	public Guid ClientId { get; set; }
	public string ClientName { get; set; } = string.Empty;
	public bool IsActive { get; set; }
}
