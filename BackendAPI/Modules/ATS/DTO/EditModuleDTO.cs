namespace ATS.DTO;

public class EditModuleDTO
{
	public int ModuleId { get; set; }
	public string? ModuleName { get; set; }
	public string? ModuleDescription { get; set; }
	public bool IsActive { get; set; }
}
