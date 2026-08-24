namespace FrontendWebassembly.DTO.ATS;

public record GetReportsResponseDTO
{
	public KeysetPaginatedResult<ReportListDTO>? Reports { get; set; }
}
