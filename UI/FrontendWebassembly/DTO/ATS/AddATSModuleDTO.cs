namespace FrontendWebassembly.DTO.ATS;

public class AddATSModuleDTO
{
	public string ModuleName { get; set; } = string.Empty;
	public string ModuleDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
}
