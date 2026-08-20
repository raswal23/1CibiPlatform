namespace FrontendWebassembly.DTO.Auth;

// Response returned by auth/editappsubrole. It intentionally mirrors the
// backend's persisted-assignment shape rather than the table's joined row.
public record AppSubRoleDTO
{
	public int AppRoleId { get; set; }
	public Guid UserId { get; set; }
	public int AppId { get; set; }
	public int Submenu { get; set; }
	public int RoleId { get; set; }
}
