namespace FrontendWebassembly.DTO.ATS;

public class GetClientAssignmentsResponseDTO
{
	public int PageIndex { get; set; }
	public int PageSize { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName("count")]
	public long TotalRecords { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName("data")]
	public List<ClientAssignmentDetailsDTO> Items { get; set; } = new();
}
