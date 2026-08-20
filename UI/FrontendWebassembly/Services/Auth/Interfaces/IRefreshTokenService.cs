namespace FrontendWebassembly.Services.Auth.Interfaces;

public interface IRefreshTokenService
{
	Task<AuthResponseDTO> GetNewAccessAndRefreshToken();

	Task<bool> Logout();
}
