namespace FrontendWebassembly.DTO.ATS;

public class AddATSUserDTO
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public int? ClientId { get; set; }
	public string Site { get; set; } = string.Empty;
	public int RoleId { get; set; }
	public HashSet<int> ModuleIds { get; set; } = new();
}
