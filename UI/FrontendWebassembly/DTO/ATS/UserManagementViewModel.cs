namespace FrontendWebassembly.DTO.ATS;

public class UserManagementViewModel
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int? ClientId { get; set; }
	public string ClientName { get; set; } = string.Empty;
	public string Site { get; set; } = string.Empty;
	public int RoleId { get; set; }
	public string RoleName { get; set; } = string.Empty;
	public HashSet<int> ModuleIds { get; set; } = new();
	public List<ModuleDetailsDTO> Modules { get; set; } = new();
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
