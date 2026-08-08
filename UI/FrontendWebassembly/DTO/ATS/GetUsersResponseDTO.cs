namespace FrontendWebassembly.DTO.ATS;

public class GetUsersResponseDTO
{
	public int PageIndex { get; set; }
	public int PageSize { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName("count")]
	public long TotalRecords { get; set; }
	[System.Text.Json.Serialization.JsonPropertyName("data")]
	public List<UserDetailsDTO> Items { get; set; } = new();
}
