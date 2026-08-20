namespace Auth.DTO;

public class ATSUserLookupDTO
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;

	// The raw name parts double as the user-directory keyset sort keys
	// (LastName, FirstName, UserId): they must survive the repository projection
	// so AuthQueries can mint the next cursor after joining them into UserName.
	public string FirstName { get; set; } = string.Empty;
	public string? MiddleName { get; set; }
	public string LastName { get; set; } = string.Empty;
}
