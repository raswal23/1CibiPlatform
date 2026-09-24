namespace EmploymentVerification.Services.ContactDirectory;

public sealed class ContactDirectoryService(IContactDirectoryRepository contactRepository)
	: IContactDirectoryService
{
	public async Task<KeysetPaginatedResult<EmploymentVerificationContactDTO>> GetContactsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken)
	{
		// An undecodable cursor (malformed, stale, or a company name that acquired a
		// '|' since it was minted) means "first page".
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);
		var afterId = Guid.TryParse(fields?[1], out var parsedId)
			? parsedId
			: (Guid?)null;
		var afterCompanyName = afterId.HasValue ? fields![0] : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await contactRepository.GetContactsPageAsync(
			paginationRequest.SearchTerm,
			afterCompanyName,
			afterId,
			pageSize + 1,
			cancellationToken);

		var (items, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(items[^1].CompanyName, items[^1].Id.ToString())
			: null;

		long? totalCount = afterCompanyName is null
			? await contactRepository.CountContactsAsync(
				paginationRequest.SearchTerm,
				cancellationToken)
			: null;

		return new KeysetPaginatedResult<EmploymentVerificationContactDTO>(
			items,
			nextCursor,
			totalCount);
	}

	public async Task<bool> AddContactAsync(
		AddEmploymentVerificationContactDTO contactDTO,
		CancellationToken cancellationToken)
	{
		var companyName = NormalizeCompanyName(contactDTO.CompanyName);
		var emailAddress = NormalizeEmailAddress(contactDTO.EmailAddress);

		await GuardAgainstDuplicateAsync(companyName, emailAddress, null, cancellationToken);

		var now = DateTime.UtcNow;

		var contact = new EmploymentVerificationContact
		{
			Id = Guid.NewGuid(),
			CompanyName = companyName,
			EmailAddress = emailAddress,
			IsActive = contactDTO.IsActive,
			CreatedAt = now,
			UpdatedAt = now
		};

		return await contactRepository.AddContactAsync(contact, cancellationToken);
	}

	public async Task<bool> EditContactAsync(
		EditEmploymentVerificationContactDTO contactDTO,
		CancellationToken cancellationToken)
	{
		var contact = await contactRepository.GetContactAsync(contactDTO.Id, cancellationToken)
			?? throw new NotFoundException($"Contact '{contactDTO.Id}' was not found.");

		var companyName = NormalizeCompanyName(contactDTO.CompanyName);
		var emailAddress = NormalizeEmailAddress(contactDTO.EmailAddress);

		await GuardAgainstDuplicateAsync(
			companyName,
			emailAddress,
			contactDTO.Id,
			cancellationToken);

		// No "in use" guard on deactivation, unlike ATS module and client management:
		// nothing references a contact row, so deactivating one cannot orphan anything.
		// If the directory is ever wired into the request flow, that guard belongs here.
		contact.CompanyName = companyName;
		contact.EmailAddress = emailAddress;
		contact.IsActive = contactDTO.IsActive;
		contact.UpdatedAt = DateTime.UtcNow;

		return await contactRepository.UpdateContactAsync(contact, cancellationToken);
	}

	/// <summary>
	/// Check-then-write. The unique index on (CompanyName, EmailAddress) is the real
	/// authority under concurrency; this exists so the ordinary case gets a 409 with a
	/// usable message instead of a 500 from a constraint violation.
	/// </summary>
	private async Task GuardAgainstDuplicateAsync(
		string companyName,
		string emailAddress,
		Guid? excludeId,
		CancellationToken cancellationToken)
	{
		var exists = await contactRepository.ContactExistsAsync(
			companyName,
			emailAddress,
			excludeId,
			cancellationToken);

		if (exists)
		{
			throw new ConflictException(
				$"'{emailAddress}' is already listed for {companyName}.");
		}
	}

	private static string NormalizeCompanyName(string companyName) =>
		companyName.Trim();

	// Lower-cased because the unique index is a plain composite index on the stored
	// values - there is no citext column and no functional index - so case folding has
	// to happen before the row is written or "HR@x.com" and "hr@x.com" both fit.
	private static string NormalizeEmailAddress(string emailAddress) =>
		emailAddress.Trim().ToLowerInvariant();
}
