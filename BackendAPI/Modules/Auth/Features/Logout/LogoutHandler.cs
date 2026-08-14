namespace Auth.Features.Logout;

public record LogoutCommand(LogoutDTO? LegacyRequest = null) : ICommand<LogoutResult>;

public record LogoutResult(bool IsLoggedOut);

public class LogoutHandler : ICommandHandler<LogoutCommand, LogoutResult>
{
	private readonly ILoginService _loginService;
	public LogoutHandler(ILoginService loginService)
	{
		this._loginService = loginService;
	}
	public async Task<LogoutResult> Handle(
		LogoutCommand request,
		CancellationToken cancellationToken)
	{
		var isLoggedOut = await this._loginService.LogoutAsync();

		return new LogoutResult(isLoggedOut);
	}
}

