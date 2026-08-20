namespace FrontendWebassembly.DTO.ATS;

public class AddATSRoleDTO
{
	public string RoleName { get; set; } = string.Empty;
	public string RoleDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
}
