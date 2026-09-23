namespace EmploymentVerification.DTO;

/// <summary>
/// A contact directory row as the console reads it. Projected in the repository
/// query rather than returning the entity so the list never materialises tracked
/// entities for a page of rows it only renders.
/// </summary>
public sealed record EmploymentVerificationContactDTO(
	Guid Id,
	string CompanyName,
	string EmailAddress,
	bool IsActive,
	DateTime CreatedAt,
	DateTime UpdatedAt);

/// <summary>Payload for creating a directory row.</summary>
public sealed record AddEmploymentVerificationContactDTO(
	string CompanyName,
	string EmailAddress,
	bool IsActive);

/// <summary>Payload for editing a directory row.</summary>
public sealed record EditEmploymentVerificationContactDTO(
	Guid Id,
	string CompanyName,
	string EmailAddress,
	bool IsActive);
