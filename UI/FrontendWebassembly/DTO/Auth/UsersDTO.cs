namespace FrontendWebassembly.DTO.Auth;

public record UsersDTO
{
	public Guid userId { get; set; }
	public string? email { get; set; }
	public string? firstName { get; set; }
	public string? middleName { get; set; }
	public string? lastName { get; set; }
	public bool isApproved { get; set; }

	// The User tab lists every registered user, so both states are shown rather than
	// filtered - see AuthRepository.BuildUsersQuery.
	public bool isActive { get; set; }
}

public record UsersResponseDTO
{
	public KeysetPaginatedResult<UsersDTO>? users { get; set; }
}