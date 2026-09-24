namespace EmploymentVerification.Data.Repository.ContactDirectory;

public sealed class ContactDirectoryRepository(EmploymentVerificationDbContext db)
	: IContactDirectoryRepository
{
	// Keyset over (CompanyName, Id) — pure query; the service decodes the cursor and
	// mints the next one. Company name alone is not unique (one company in the seed
	// data has 180 mailboxes), so Id breaks the tie and the ORDER BY must match the
	// IX_..._CompanyName_Id index exactly.
	public async Task<List<EmploymentVerificationContactDTO>> GetContactsPageAsync(
		string? searchTerm,
		string? afterCompanyName,
		Guid? afterId,
		int take,
		CancellationToken cancellationToken)
	{
		var query = BuildContactsQuery(searchTerm);

		if (afterCompanyName is not null && afterId.HasValue)
		{
			// Hoisted out of the lambda: closing over afterId.Value hands EF a
			// Nullable<Guid>.Value node to translate.
			var seekId = afterId.Value;

			query = query.Where(contact =>
				string.Compare(contact.CompanyName, afterCompanyName) > 0
				|| (contact.CompanyName == afterCompanyName
					&& contact.Id.CompareTo(seekId) > 0));
		}

		return await query
			.OrderBy(contact => contact.CompanyName)
			.ThenBy(contact => contact.Id)
			.Take(take)
			.Select(contact => new EmploymentVerificationContactDTO(
				contact.Id,
				contact.CompanyName,
				contact.EmailAddress,
				contact.IsActive,
				contact.CreatedAt,
				contact.UpdatedAt))
			.ToListAsync(cancellationToken);
	}

	public Task<long> CountContactsAsync(
		string? searchTerm,
		CancellationToken cancellationToken) =>
		BuildContactsQuery(searchTerm).LongCountAsync(cancellationToken);

	public async Task<IReadOnlySet<string>> GetKnownActiveMailboxesAsync(
		IReadOnlyCollection<string> emailAddresses,
		CancellationToken cancellationToken)
	{
		// Contacts are stored lower-cased by ContactDirectoryService, so the candidate's
		// address is folded the same way before comparing - otherwise a form entry of
		// "HR@Acme.com" would miss a directory row of "hr@acme.com".
		var addresses = emailAddresses
			.Where(address => !string.IsNullOrWhiteSpace(address))
			.Select(address => address.Trim().ToLowerInvariant())
			.Distinct()
			.ToList();

		if (addresses.Count == 0)
		{
			return new HashSet<string>();
		}

		var known = await db.Contacts
			.AsNoTracking()
			.Where(contact => contact.IsActive)
			.Where(contact => addresses.Contains(contact.EmailAddress))
			.Select(contact => contact.EmailAddress)
			.ToListAsync(cancellationToken);

		return known.ToHashSet();
	}

	public Task<bool> ContactExistsAsync(
		string companyName,
		string emailAddress,
		Guid? excludeId,
		CancellationToken cancellationToken) =>
		db.Contacts
			.AsNoTracking()
			.AnyAsync(
				contact =>
					contact.CompanyName == companyName
					&& contact.EmailAddress == emailAddress
					&& (excludeId == null || contact.Id != excludeId),
				cancellationToken);

	// Tracked on purpose: the service mutates the returned entity and calls
	// UpdateContactAsync, so change tracking is what produces the UPDATE.
	public Task<EmploymentVerificationContact?> GetContactAsync(
		Guid id,
		CancellationToken cancellationToken) =>
		db.Contacts.FirstOrDefaultAsync(contact => contact.Id == id, cancellationToken);

	public async Task<bool> AddContactAsync(
		EmploymentVerificationContact contact,
		CancellationToken cancellationToken)
	{
		await db.Contacts.AddAsync(contact, cancellationToken);
		await db.SaveChangesAsync(cancellationToken);

		return true;
	}

	public async Task<bool> UpdateContactAsync(
		EmploymentVerificationContact contact,
		CancellationToken cancellationToken)
	{
		db.Contacts.Update(contact);
		await db.SaveChangesAsync(cancellationToken);

		return true;
	}

	// Search spans both columns because a user looking for a contact knows either the
	// company or the mailbox, rarely which one the row was filed under.
	private IQueryable<EmploymentVerificationContact> BuildContactsQuery(string? searchTerm)
	{
		var query = db.Contacts.AsNoTracking();

		if (!string.IsNullOrEmpty(searchTerm))
		{
			var term = $"%{searchTerm}%";

			query = query.Where(contact =>
				EF.Functions.ILike(contact.CompanyName, term)
				|| EF.Functions.ILike(contact.EmailAddress, term));
		}

		return query;
	}
}
