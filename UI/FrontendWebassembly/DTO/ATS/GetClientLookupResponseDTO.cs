namespace FrontendWebassembly.DTO.ATS;

public class GetClientLookupResponseDTO
{
	public int PageIndex { get; set; }
	public int PageSize { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName("count")]
	public long TotalRecords { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName("data")]
	public List<ClientLookupDTO> Items { get; set; } = new();
}
