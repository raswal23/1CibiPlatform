namespace Auth.DTO;

/// <summary>
/// The authenticated user's own profile. Returned by the profile query and by a
/// successful profile update so the caller can refresh what it displays.
/// </summary>
public class UserProfileDTO
{
	public Guid UserId { get; set; }

	public string Email { get; set; } = string.Empty;

	public string FirstName { get; set; } = string.Empty;

	public string? MiddleName { get; set; }

	public string LastName { get; set; } = string.Empty;

	public string FullName { get; set; } = string.Empty;
}
