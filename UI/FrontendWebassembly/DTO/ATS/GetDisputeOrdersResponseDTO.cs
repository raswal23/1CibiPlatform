namespace FrontendWebassembly.DTO.ATS;

public record GetDisputeOrdersResponseDTO
{
	public PaginatedResult<DisputeOrderListDTO>? Orders { get; set; }
}
