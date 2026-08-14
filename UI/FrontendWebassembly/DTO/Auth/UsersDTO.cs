namespace FrontendWebassembly.DTO.Auth;

public record UsersDTO
{
	public Guid userId { get; set; }
	public string? email { get; set; }
	public string? firstName { get; set; }
	public string? middleName { get; set; }
	public string? lastName { get; set; }
	public bool isApproved { get; set; }
}

public record UsersResponseDTO
{
	public PaginatedResult<UsersDTO>? users { get; set; }
}