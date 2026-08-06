namespace FrontendWebassembly.DTO.ATS;

public class GetClientsResponseDTO
{
	public int PageIndex { get; set; }
	public int PageSize { get; set; }
	public long TotalRecords { get; set; }
	public List<ClientDetailsDTO> Items { get; set; } = new();
}
