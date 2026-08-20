namespace FrontendWebassembly.DTO.Auth;

public record AppSubRolesDTO
{
	public int AppRoleId { get; set; }
	public Guid UserId { get; set; }
	public string? UserEmail { get; set; }
	public int AppId { get; set; }
	public string? AppName { get; set; }
	public int SubMenuId { get; set; }
	public string? SubMenuName { get; set; }
	public int RoleId { get; set; }
	public string? RoleName { get; set; }
}

public record AppSubRolesResponseDTO
{
	public KeysetPaginatedResult<AppSubRolesDTO>? appsubroles { get; set; }
}