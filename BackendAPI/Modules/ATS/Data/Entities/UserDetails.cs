namespace ATS.Data.Entities;

public class UserDetails
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int? ClientId { get; set; }
	public string Site { get; set; } = string.Empty;
	public int RoleId { get; set; }
	public int ModuleId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public RoleDetails Role { get; set; } = null!;
	public ModuleDetails Module { get; set; } = null!;
}
