namespace FrontendWebassembly.DTO.Auth;

public record EditAppSubRoleDTO
{
	public int AppSubRoleId { get; set; }
	public Guid UserId { get; set; }
	public int AppId { get; set; }
	public int SubMenuId { get; set; }
	public int RoleId { get; set; }
}
