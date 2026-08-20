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

	// The opaque-cursor boundary for cross-module callers (ATS client assignments).
	// Decodes the cursor, walks the (LastName, FirstName, Id) keyset, and mints the
	// next cursor; an undecodable cursor (malformed, stale) means "first page".
	public async Task<KeysetPaginatedResult<ATSUserLookupDTO>> GetATSAssignedUsersAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 3);
		Guid? afterId = Guid.TryParse(fields?[2], out var userId) ? userId : null;
		var (afterLastName, afterFirstName) = afterId.HasValue ? (fields![0], fields[1]) : (null, null);
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _authRepository.GetATSAssignedUsersPageAsync(
			paginationRequest.SearchTerm, afterLastName, afterFirstName,
			afterLastName is null ? null : afterId, pageSize + 1, cancellationToken);
		var (page, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(page[^1].LastName, page[^1].FirstName, page[^1].UserId.ToString("D"))
			: null;
		long? totalCount = afterLastName is null
			? await _authRepository.CountATSAssignedUsersAsync(paginationRequest.SearchTerm, cancellationToken)
			: null;

		// The repository leaves UserName empty (the join is not EF-translatable); fill it
		// here into fresh instances so cached first-page rows are never mutated in place.
		var data = page.Select(user => new ATSUserLookupDTO
		{
			UserId = user.UserId,
			UserEmail = user.UserEmail,
			FirstName = user.FirstName,
			MiddleName = user.MiddleName,
			LastName = user.LastName,
			UserName = string.Join(" ", new[] { user.FirstName, user.MiddleName, user.LastName }
				.Where(name => !string.IsNullOrWhiteSpace(name)))
		}).ToArray();

		return new KeysetPaginatedResult<ATSUserLookupDTO>(data, nextCursor, totalCount);
	}

	public Task<ATSUserLookupDTO?> GetATSAssignedUserAsync(
		Guid userId,
		CancellationToken cancellationToken) =>
		_authRepository.GetATSAssignedUserAsync(userId, cancellationToken);

	public Task<IReadOnlyDictionary<string, Guid>> GetUserIdsByEmailAsync(
		IReadOnlyCollection<string> emails,
		CancellationToken cancellationToken) =>
		_authRepository.GetUserIdsByEmailAsync(emails, cancellationToken);
}
