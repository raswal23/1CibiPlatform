namespace EmploymentVerification.Data.Repository.ContactDirectory;

/// <summary>
/// Persistence for the HR contact directory. A focused contract rather than growth
/// of <see cref="IEmploymentVerificationRepository"/>: the two are unrelated business
/// areas that happen to share a schema, and each has its own cache decorator.
/// </summary>
public interface IContactDirectoryRepository
{
	/// <summary>
	/// One keyset page ordered by (CompanyName, Id). Callers pass
	/// <c>take = pageSize + 1</c>; the extra row only signals that a next page exists.
	/// </summary>
	Task<List<EmploymentVerificationContactDTO>> GetContactsPageAsync(
		string? searchTerm,
		string? afterCompanyName,
		Guid? afterId,
		int take,
		CancellationToken cancellationToken);

	Task<long> CountContactsAsync(
		string? searchTerm,
		CancellationToken cancellationToken);

	/// <summary>
	/// Whether the (company, mailbox) pair is already listed. <paramref name="excludeId"/>
	/// is the row being edited, so renaming a contact without changing its identity
	/// does not collide with itself.
	/// </summary>
	Task<bool> ContactExistsAsync(
		string companyName,
		string emailAddress,
		Guid? excludeId,
		CancellationToken cancellationToken);

	Task<EmploymentVerificationContact?> GetContactAsync(
		Guid id,
		CancellationToken cancellationToken);

	Task<bool> AddContactAsync(
		EmploymentVerificationContact contact,
		CancellationToken cancellationToken);

	Task<bool> UpdateContactAsync(
		EmploymentVerificationContact contact,
		CancellationToken cancellationToken);
}
