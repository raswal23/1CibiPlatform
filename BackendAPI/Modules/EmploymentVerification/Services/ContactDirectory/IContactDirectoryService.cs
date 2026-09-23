namespace EmploymentVerification.Services.ContactDirectory;

public interface IContactDirectoryService
{
	/// <summary>
	/// One keyset page of the directory, ordered by company name. The total count is
	/// returned on the first page only - a cursor page has no cheap way to know it and
	/// the caller reuses the first page's value for the whole walk.
	/// </summary>
	Task<KeysetPaginatedResult<EmploymentVerificationContactDTO>> GetContactsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	/// <summary>
	/// Adds a directory row. Throws <see cref="ConflictException"/> when the mailbox is
	/// already listed for that company.
	/// </summary>
	Task<bool> AddContactAsync(
		AddEmploymentVerificationContactDTO contactDTO,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates a directory row, including deactivating it. Throws
	/// <see cref="NotFoundException"/> when the row is gone and
	/// <see cref="ConflictException"/> when the edit would duplicate another row.
	/// </summary>
	Task<bool> EditContactAsync(
		EditEmploymentVerificationContactDTO contactDTO,
		CancellationToken cancellationToken);
}
