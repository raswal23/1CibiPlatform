namespace FrontendWebassembly.DTO.ATS;

public class GetClientsResponseDTO
{
	public int PageIndex { get; set; }
	public int PageSize { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName("count")]
	public long TotalRecords { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName("data")]
	public List<ClientDetailsDTO> Items { get; set; } = new();
}
