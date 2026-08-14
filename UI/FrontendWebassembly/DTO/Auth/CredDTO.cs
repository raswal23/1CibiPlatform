namespace FrontendWebassembly.DTO.Auth;

public record LoginCred(
	string Email,
	string Password,
	bool IsRememberMe);