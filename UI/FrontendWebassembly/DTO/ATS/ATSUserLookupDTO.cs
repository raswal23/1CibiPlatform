namespace FrontendWebassembly.DTO.ATS;

public class ATSUserLookupDTO
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;

	// Mirrors the API contract: the name parts are also the user-directory keyset
	// sort keys. UserName is what the UI displays; these are carried for parity.
	public string FirstName { get; set; } = string.Empty;
	public string? MiddleName { get; set; }
	public string LastName { get; set; } = string.Empty;
}
