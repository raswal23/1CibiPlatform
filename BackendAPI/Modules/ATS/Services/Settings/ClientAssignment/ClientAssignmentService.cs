namespace ATS.Services.Settings.ClientAssignment;

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

	public async Task<KeysetPaginatedResult<ClientAssignmentDetailsDTO>> GetAssignmentsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var users = await _authQueries.GetATSAssignedUsersAsync(
			paginationRequest,
			cancellationToken);
		var pageUsers = users.Items.ToArray();
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

		// The Auth page drives the walk: its cursor and count pass straight through.
		return new KeysetPaginatedResult<ClientAssignmentDetailsDTO>(
			data,
			users.NextCursor,
			users.TotalCount);
	}

	public async Task<KeysetPaginatedResult<ClientLookupDTO>> GetAssignableClientsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		// An undecodable cursor (malformed, stale) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);
		int? afterClientId = int.TryParse(fields?[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var clientId) ? clientId : null;
		var afterClientName = afterClientId.HasValue ? fields![0] : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _userClientRepository.GetAssignableClientsPageAsync(
			paginationRequest.SearchTerm, afterClientName, afterClientName is null ? null : afterClientId,
			pageSize + 1, cancellationToken);
		var (clients, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(clients[^1].ClientName,
				clients[^1].ClientId.ToString(CultureInfo.InvariantCulture))
			: null;
		long? totalCount = afterClientName is null
			? await _userClientRepository.CountAssignableClientsAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		return new KeysetPaginatedResult<ClientLookupDTO>(clients, nextCursor, totalCount);
	}

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
