namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(CancellationToken cancellationToken) =>
		GetAssignmentsAsync(cancellationToken);

	private async Task<IReadOnlyList<UserClientDetailsDTO>> GetAssignmentsAsync(CancellationToken cancellationToken) =>
		await _hybridCache.GetOrCreateAsync<List<UserClientDetailsDTO>>(
			"user_client_assignments",
			async token => (await _atsRepository.GetUserClientAssignmentsAsync(token)).ToList(),
			tags: [CacheTags.UserClient], cancellationToken: cancellationToken);

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		int clientId,
		CancellationToken cancellationToken) =>
		await _hybridCache.GetOrCreateAsync<List<UserClientDetailsDTO>>(
			$"user_client_assignments_client_{clientId}",
			async token => (await _atsRepository.GetUserClientAssignmentsAsync(clientId, token)).ToList(),
			tags: [CacheTags.UserClient], cancellationToken: cancellationToken);

	public Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		IReadOnlyCollection<Guid> userIds,
		CancellationToken cancellationToken) =>
		_atsRepository.GetUserClientAssignmentsAsync(userIds, cancellationToken);

	public Task<List<ClientLookupDTO>> GetAssignableClientsPageAsync(
		string? searchTerm, string? afterClientName, int? afterClientId, int take,
		CancellationToken cancellationToken) =>
		_atsRepository.GetAssignableClientsPageAsync(searchTerm, afterClientName, afterClientId, take, cancellationToken);

	public Task<long> CountAssignableClientsAsync(string? searchTerm, CancellationToken cancellationToken) =>
		_atsRepository.CountAssignableClientsAsync(searchTerm, cancellationToken);

	public Task<bool> ClientIsActiveAsync(int clientId, CancellationToken cancellationToken) =>
		_atsRepository.ClientIsActiveAsync(clientId, cancellationToken);

	public Task<UserClientDetails?> GetUserClientAssignmentAsync(Guid userId, CancellationToken cancellationToken) =>
		_hybridCache.GetOrCreateAsync<UserClientDetails?>(
			$"user_client_assignment_{userId}",
			async token => await _atsRepository.GetUserClientAssignmentAsync(userId, token),
			tags: [CacheTags.UserClient], cancellationToken: cancellationToken).AsTask();

	public async Task<UserClientDetails> AssignUserClientAsync(AssignUserClientDTO assignment, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.AssignUserClientAsync(assignment, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.UserClient, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.User, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.Package, cancellationToken);
		return result;
	}
}
