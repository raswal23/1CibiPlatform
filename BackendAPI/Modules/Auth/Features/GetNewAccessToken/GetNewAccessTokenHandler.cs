namespace Auth.Features.GetNewAccessToken;

public record GetNewAccessTokenCommand() : ICommand<GetNewAccessTokenResult>;

public record GetNewAccessTokenResult(LoginResponseWebDTO loginResponseWebDTO);

public class GetNewAccessTokenHandler : ICommandHandler<GetNewAccessTokenCommand, GetNewAccessTokenResult>
{
	private readonly IRefreshTokenService _refreshTokenService;

	public GetNewAccessTokenHandler(IRefreshTokenService refreshTokenService)
	{
		this._refreshTokenService = refreshTokenService;
	}

	public async Task<GetNewAccessTokenResult> Handle(
		GetNewAccessTokenCommand request,
		CancellationToken cancellationToken)
	{
		var newSetOfTokens = await this._refreshTokenService
			.GetNewAccessTokenAsync();

		return new GetNewAccessTokenResult(newSetOfTokens);
	}
}
