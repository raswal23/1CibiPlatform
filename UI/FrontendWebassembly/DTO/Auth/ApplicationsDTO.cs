namespace FrontendWebassembly.DTO.Auth;

public record ApplicationsDTO
{
	public int applicationId { get; set; }
	public string? applicationName { get; set; }
	public string? Description { get; set; }
	public bool IsActive { get; set; }
}

public record ApplicationsResponseDTO
{
	public KeysetPaginatedResult<ApplicationsDTO>? applications { get; set; }
}