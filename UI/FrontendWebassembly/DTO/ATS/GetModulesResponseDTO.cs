namespace FrontendWebassembly.DTO.ATS;

public class GetModulesResponseDTO
{
	public KeysetPaginatedResult<ModuleDetailsDTO>? Modules { get; set; }
}
