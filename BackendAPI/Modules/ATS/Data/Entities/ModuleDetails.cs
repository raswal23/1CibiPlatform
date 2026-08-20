namespace ATS.Data.Entities;

public class ModuleDetails
{
	public int ModuleId { get; set; }
	public string ModuleName { get; set; } = string.Empty;
	public string ModuleDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
