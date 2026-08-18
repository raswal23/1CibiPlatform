namespace Auth.Services;

public interface ILoginService
{
	Task<LoginResponseDTO> LoginAsync(string username, string password);

	Task<LoginResponseWebDTO> LoginWebAsync(LoginWebCred cred);

	Task<bool> LogoutAsync();

	Task<bool> IsAuthenticated();
}
