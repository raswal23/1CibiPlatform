namespace ATS.Services;

public sealed class ClientAssignmentService : IClientAssignmentService
{
	private readonly IATSUserRepository _userRepository;
	private readonly IAuthQueries _authQueries;

	public ClientAssignmentService(
		IATSUserRepository userRepository,
		IAuthQueries authQueries)
	{
		_userRepository = userRepository;
		_authQueries = authQueries;
	}

	public async Task<PaginatedResult<ClientAssignmentDetailsDTO>> GetAssignmentsAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var users = await _authQueries.GetATSAssignedUsersAsync(
			paginationRequest,
			cancellationToken);
		var pageUsers = users.Data.ToArray();
		var assignments = await _userRepository.GetUserClientAssignmentsAsync(
			pageUsers.Select(user => user.UserId).ToArray(),
			cancellationToken);
		var assignmentLookup = assignments.ToDictionary(assignment => assignment.UserId);

		var data = pageUsers.Select(user =>
		{
			assignmentLookup.TryGetValue(user.UserId, out var assignment);
			return new ClientAssignmentDetailsDTO
			{
				UserId = user.UserId,
				UserName = user.UserName,
				UserEmail = user.UserEmail,
				ClientId = assignment?.ClientId,
				ClientName = assignment?.ClientName,
				AssignedAt = assignment?.CreatedAt,
				UpdatedAt = assignment?.UpdatedAt
			};
		}).ToArray();

		return new PaginatedResult<ClientAssignmentDetailsDTO>(
			users.PageIndex,
			users.PageSize,
			users.Count,
			data);
	}

	public Task<PaginatedResult<ClientLookupDTO>> GetAssignableClientsAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken) =>
		_userRepository.GetAssignableClientsAsync(paginationRequest, cancellationToken);

	public async Task<ClientAssignmentDetailsDTO> AssignClientAsync(
		AssignUserClientDTO assignment,
		CancellationToken cancellationToken)
	{
		var user = await _authQueries.GetATSAssignedUserAsync(
			assignment.UserId,
			cancellationToken)
			?? throw new BadRequestException(
				"The selected user is not an active Auth user assigned to ATS.");

		await _userRepository.AssignUserClientAsync(assignment, cancellationToken);
		var persisted = (await _userRepository.GetUserClientAssignmentsAsync(
			[assignment.UserId],
			cancellationToken)).Single();

		return new ClientAssignmentDetailsDTO
		{
			UserId = user.UserId,
			UserName = user.UserName,
			UserEmail = user.UserEmail,
			ClientId = persisted.ClientId,
			ClientName = persisted.ClientName,
			AssignedAt = persisted.CreatedAt,
			UpdatedAt = persisted.UpdatedAt
		};
	}
}
