namespace FrontendWebassembly.DTO.EmploymentVerification;

/// <summary>
/// A contact directory row as the console renders it. Mirrors the API's
/// <c>EmploymentVerificationContactDTO</c>.
/// </summary>
public sealed class EmploymentVerificationContactDTO
{
	public Guid Id { get; set; }
	public string CompanyName { get; set; } = "";
	public string EmailAddress { get; set; } = "";
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// The <c>GET /employmentverification/getcontacts</c> envelope. The API wraps the page
/// in a named property, so the service projects it away rather than exposing the
/// wrapper to pages.
/// </summary>
public sealed class GetContactsResponseDTO
{
	public KeysetPaginatedResult<EmploymentVerificationContactDTO>? Contacts { get; set; }
}

public sealed class AddEmploymentVerificationContactDTO
{
	public string CompanyName { get; set; } = "";
	public string EmailAddress { get; set; } = "";
	public bool IsActive { get; set; } = true;
}

public sealed class EditEmploymentVerificationContactDTO
{
	public Guid Id { get; set; }
	public string CompanyName { get; set; } = "";
	public string EmailAddress { get; set; } = "";
	public bool IsActive { get; set; } = true;
}
