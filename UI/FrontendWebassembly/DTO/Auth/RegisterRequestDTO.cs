namespace FrontendWebassembly.DTO.Auth;

public record RegisterRequestDTO(
string Email,
string PasswordHash,
string FirstName,
string LastName,
string? MiddleName);

