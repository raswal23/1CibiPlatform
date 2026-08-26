namespace Auth.DTO;

/// <summary>
/// Name fields a user may change on their own profile. The identity of the user
/// being updated is never taken from this payload — it always comes from the
/// authenticated principal, so a caller cannot rename somebody else.
/// </summary>
public class UpdateUserProfileDTO
{
	public string? FirstName { get; set; }

	public string? MiddleName { get; set; }

	public string? LastName { get; set; }
}
