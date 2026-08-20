namespace ATS.DTO;

public class AddModuleDTO
{
	public string? ModuleName { get; set; }
	public string? ModuleDescription { get; set; }
	public bool IsActive { get; set; } = true;
}
