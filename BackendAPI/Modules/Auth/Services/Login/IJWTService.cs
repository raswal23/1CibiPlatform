namespace Auth.Service;

public interface IJWTService
{
	string GetAccessToken(LoginDTO loginDTO, int? sessionId = null);
}
