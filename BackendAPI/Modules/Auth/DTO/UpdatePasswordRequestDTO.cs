namespace Auth.DTO;

public record UpdatePasswordRequestDTO(
	string hashToken,
	string newPassword);

