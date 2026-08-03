namespace FrontendWebassembly.DTO.ATS;

public record GetReportsResponseDTO
{
	public PaginatedResult<ReportListDTO>? Reports { get; set; }
}
