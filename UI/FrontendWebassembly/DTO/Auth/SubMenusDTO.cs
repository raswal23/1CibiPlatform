namespace FrontendWebassembly.DTO.Auth;

public record SubMenusDTO
{
	public int subMenuId { get; set; }
	public string? subMenuName { get; set; }
	public string? Description { get; set; }
	public bool IsActive { get; set; }
}

public record SubMenusResponseDTO
{
	public KeysetPaginatedResult<SubMenusDTO>? submenus { get; set; }
}