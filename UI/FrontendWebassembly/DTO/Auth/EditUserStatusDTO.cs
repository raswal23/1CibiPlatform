namespace FrontendWebassembly.DTO.Auth;

/// <summary>
/// Changes only whether a user account is active. Approval and the user's own details are
/// deliberately out of reach of this path.
/// </summary>
public record EditUserStatusDTO
{
	public Guid UserId { get; set; }

	public bool IsActive { get; set; }
}
