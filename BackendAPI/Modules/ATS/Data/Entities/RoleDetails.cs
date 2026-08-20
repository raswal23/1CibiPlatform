namespace ATS.Data.Entities;

public class RoleDetails
{
	public int RoleId { get; set; }
	public string RoleName { get; set; } = string.Empty;
	public string RoleDescription { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
