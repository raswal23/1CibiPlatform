namespace Auth.Services;

public interface IRefreshTokenService
{
	(string, string) GenerateRefreshToken();

	bool ValidateHashToken(string providedToken, string storedHash);

	Task<string> RevokeTokenAsync();

	Task<LoginResponseWebDTO> GetNewAccessTokenAsync();

	string HashToken(string token);
}
