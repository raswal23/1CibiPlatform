namespace FrontendWebassembly.DTO.Auth;

public record RolesDTO
{
	public int roleId { get; set; }
	public string? roleName { get; set; }
	public string? Description { get; set; }
}

public record RolesResponseDTO
{
	public KeysetPaginatedResult<RolesDTO>? roles { get; set; }
}