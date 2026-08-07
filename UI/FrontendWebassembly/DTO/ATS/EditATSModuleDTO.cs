namespace FrontendWebassembly.DTO.ATS;

public class EditATSModuleDTO
{
	public int ModuleId { get; set; }
	public string ModuleName { get; set; } = string.Empty;
	public string ModuleDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
}
