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

	public Task<PaginatedResult<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken) =>
		_authRepository.GetATSAssignedUsersAsync(paginationRequest, cancellationToken);

	public Task<ATSUserLookupDTO?> GetATSAssignedUserAsync(
		Guid userId,
		CancellationToken cancellationToken) =>
		_authRepository.GetATSAssignedUserAsync(userId, cancellationToken);
}
