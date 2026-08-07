namespace ATS.DTO;

public class AddRoleDTO
{
	public string? RoleName { get; set; }
	public string? RoleDescription { get; set; }
	public bool IsActive { get; set; } = true;
}
