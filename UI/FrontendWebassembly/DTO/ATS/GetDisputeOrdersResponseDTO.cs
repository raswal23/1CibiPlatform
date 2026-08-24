namespace FrontendWebassembly.DTO.ATS;

public record GetDisputeOrdersResponseDTO
{
	public KeysetPaginatedResult<DisputeOrderListDTO>? Orders { get; set; }
}
