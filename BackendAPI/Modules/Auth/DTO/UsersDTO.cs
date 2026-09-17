namespace Auth.DTO;

public record UsersDTO(
	Guid userId,
	string email,
	string firstName,
	string? middleName,
	string lastName,
	bool isApproved,
	bool isActive);
