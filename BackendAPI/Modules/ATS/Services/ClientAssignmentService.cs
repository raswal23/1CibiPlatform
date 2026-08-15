namespace ATS.Services;

public sealed class ClientAssignmentService : IClientAssignmentService
{
	private readonly IUserClientRepository _userClientRepository;
	private readonly IAuthQueries _authQueries;

	public ClientAssignmentService(
		IUserClientRepository userClientRepository,
		IAuthQueries authQueries)
	{
		_userClientRepository = userClientRepository;
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
		var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
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
		_userClientRepository.GetAssignableClientsAsync(paginationRequest, cancellationToken);

	public async Task<ClientAssignmentDetailsDTO> AssignClientAsync(
		AssignUserClientDTO assignment,
		CancellationToken cancellationToken)
	{
		var user = await _authQueries.GetATSAssignedUserAsync(
			assignment.UserId,
			cancellationToken)
			?? throw new BadRequestException(
				"The selected user is not an active Auth user assigned to ATS.");

		if (!await _userClientRepository.ClientIsActiveAsync(assignment.ClientId, cancellationToken))
			throw new BadRequestException("The selected client does not exist or is inactive.");

		await _userClientRepository.AssignUserClientAsync(assignment, cancellationToken);
		var persisted = (await _userClientRepository.GetUserClientAssignmentsAsync(
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
