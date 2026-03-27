namespace FrontendWebassembly.DTO.Auth;

public record UpdatePasswordRequestDTO(
	string hashToken,
	string newPassword);
