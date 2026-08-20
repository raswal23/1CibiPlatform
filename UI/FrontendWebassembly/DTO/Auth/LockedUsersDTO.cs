namespace FrontendWebassembly.DTO.Auth;

public record LockedUsersDTO
{
	public Guid UserId { get; set; }
	public string? Email { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime LockReleaseAt { get; set; }
}

public record LockedUsersResponseDTO
{
	public KeysetPaginatedResult<LockedUsersDTO>? lockedusers { get; set; }
}
