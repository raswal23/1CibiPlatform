namespace FrontendWebassembly.DTO.Auth;

public record EditRoleDTO
{
	public int RoleId { get; set; }
	public string? RoleName { get; set; }
	public string? Description { get; set; }
}
