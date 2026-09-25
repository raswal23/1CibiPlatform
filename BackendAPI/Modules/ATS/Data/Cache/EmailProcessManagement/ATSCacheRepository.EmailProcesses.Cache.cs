namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	// One key, no parameters: the read is the whole table. See the note on the contract for
	// why it is unpaged.
	public Task<List<EmailProcessDetailsDTO>> GetEmailProcessesAsync(CancellationToken cancellationToken) =>
		_hybridCache.GetOrCreateAsync<List<EmailProcessDetailsDTO>>(
			"emailprocess_v1_all",
			async token => await _atsRepository.GetEmailProcessesAsync(token),
			tags: [CacheTags.EmailProcess],
			cancellationToken: cancellationToken).AsTask();

	// Uncached, like GetPackageAsync: this returns a TRACKED entity for an edit to mutate, and
	// a cached instance would be a detached object shared between requests - the second edit
	// would save the first one's changes.
	public Task<EmailProcessDetails?> GetEmailProcessAsync(int id, CancellationToken cancellationToken) =>
		_atsRepository.GetEmailProcessAsync(id, cancellationToken);

	// Uniqueness guard - must see the current rows, never a cached answer, or two callers
	// adding the same process in the same window both pass and the second dies on the index.
	public Task<bool> EmailProcessExistsAsync(string emailProcess, CancellationToken cancellationToken) =>
		_atsRepository.EmailProcessExistsAsync(emailProcess, cancellationToken);

	public async Task<EmailProcessDetails> AddEmailProcessAsync(
		EmailProcessDetails emailProcess,
		CancellationToken cancellationToken)
	{
		var result = await _atsRepository.AddEmailProcessAsync(emailProcess, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.EmailProcess, cancellationToken);

		return result;
	}

	public async Task<EmailProcessDetails> EditEmailProcessAsync(
		EmailProcessDetails emailProcess,
		CancellationToken cancellationToken)
	{
		var result = await _atsRepository.EditEmailProcessAsync(emailProcess, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.EmailProcess, cancellationToken);

		return result;
	}
}
