namespace FrontendWebassembly.DTO.Auth;

public record UnApprovedUsersDTO
{
	public Guid userId { get; set; }
	public string? email { get; set; }
	public string? firstName { get; set; }
	public string? middleName { get; set; }
	public string? lastName { get; set; }
	public bool isApproved { get; set; }
}

public record UnApprovedUsersResponseDTO
{
	public KeysetPaginatedResult<UnApprovedUsersDTO>? users { get; set; }
}