namespace EmploymentVerification.Data.Cache.ContactDirectory;

/// <summary>
/// HybridCache decorator for the contact directory, applied with Scrutor in
/// <c>EmploymentVerificationServiceConfiguration</c>.
/// </summary>
/// <remarks>
/// Its own tag, separate from the requests decorator: raising a verification request
/// must not evict the contacts page, and editing a contact must not evict the
/// tracking list.
/// </remarks>
public sealed class ContactDirectoryCacheRepository(
	IContactDirectoryRepository repository,
	HybridCache cache) : IContactDirectoryRepository
{
	private const string ContactsTag = "employmentverification:contacts";

	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository. `take` is part of the
	// key because page size 10 and page size 50 are different first pages.
	public Task<List<EmploymentVerificationContactDTO>> GetContactsPageAsync(
		string? searchTerm,
		string? afterCompanyName,
		Guid? afterId,
		int take,
		CancellationToken cancellationToken)
	{
		if (afterCompanyName is not null)
		{
			return repository.GetContactsPageAsync(
				searchTerm,
				afterCompanyName,
				afterId,
				take,
				cancellationToken);
		}

		var key = $"evcontact_first_take_{take}_search_{searchTerm}";

		return cache.GetOrCreateAsync(
			key,
			async token => await repository.GetContactsPageAsync(
				searchTerm,
				null,
				null,
				take,
				token),
			tags: [ContactsTag],
			cancellationToken: cancellationToken).AsTask();
	}

	// Shares the tag with the first page so one invalidation clears both. The UI reads
	// TotalCount on page 0 only and reuses it for the whole walk, so a stale count
	// would survive until the next reset.
	public Task<long> CountContactsAsync(
		string? searchTerm,
		CancellationToken cancellationToken) =>
		cache.GetOrCreateAsync(
			$"evcontact_count_search_{searchTerm}",
			async token => await repository.CountContactsAsync(searchTerm, token),
			tags: [ContactsTag],
			cancellationToken: cancellationToken).AsTask();

	// Deliberately uncached: this decides whether an address may be written to at all,
	// so a cached answer could keep sending to a contact an operator had just
	// deactivated, or keep refusing one they had just added.
	public Task<IReadOnlySet<string>> GetKnownActiveMailboxesAsync(
		IReadOnlyCollection<string> emailAddresses,
		CancellationToken cancellationToken) =>
		repository.GetKnownActiveMailboxesAsync(emailAddresses, cancellationToken);

	// Deliberately uncached: this is the duplicate guard behind the 409, and a cached
	// answer would let a second row through the window before the tag was evicted.
	public Task<bool> ContactExistsAsync(
		string companyName,
		string emailAddress,
		Guid? excludeId,
		CancellationToken cancellationToken) =>
		repository.ContactExistsAsync(companyName, emailAddress, excludeId, cancellationToken);

	// Deliberately uncached: the service mutates the entity this returns, so it must
	// be a live tracked instance rather than a shared cached one.
	public Task<EmploymentVerificationContact?> GetContactAsync(
		Guid id,
		CancellationToken cancellationToken) =>
		repository.GetContactAsync(id, cancellationToken);

	public async Task<bool> AddContactAsync(
		EmploymentVerificationContact contact,
		CancellationToken cancellationToken)
	{
		var result = await repository.AddContactAsync(contact, cancellationToken);

		if (result)
		{
			await cache.RemoveByTagAsync(ContactsTag, cancellationToken);
		}

		return result;
	}

	public async Task<bool> UpdateContactAsync(
		EmploymentVerificationContact contact,
		CancellationToken cancellationToken)
	{
		var result = await repository.UpdateContactAsync(contact, cancellationToken);

		if (result)
		{
			await cache.RemoveByTagAsync(ContactsTag, cancellationToken);
		}

		return result;
	}
}
