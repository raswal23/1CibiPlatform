namespace Auth.Shared.Implementations;

internal class AuthQueries : IAuthQueries
{
	private readonly IAuthRepository _authRepository;

	public AuthQueries(IAuthRepository authRepository)
	{
		_authRepository = authRepository;
	}

	public async Task<IReadOnlyList<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
		CancellationToken cancellationToken)
	{
		return await _authRepository.GetATSAssignedUsersAsync(cancellationToken);
	}
}
