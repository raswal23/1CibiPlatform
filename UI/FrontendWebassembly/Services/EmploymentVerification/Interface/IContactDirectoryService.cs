namespace FrontendWebassembly.Services.EmploymentVerification.Interface;

/// <summary>
/// The HR contact directory API. A focused service beside
/// <see cref="IEmploymentVerificationService"/> rather than more methods on it: the
/// two cover unrelated business areas, and this one returns
/// <c>ServiceResponse&lt;T&gt;</c> so it composes with
/// <c>CrudPageBase.LoadCursorPagedDataAsync</c> the way the ATS management boards do.
/// </summary>
public interface IContactDirectoryService
{
	Task<ServiceResponse<KeysetPaginatedResult<EmploymentVerificationContactDTO>>> GetContactsAsync(
		string? cursor,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<bool>> AddContactAsync(
		AddEmploymentVerificationContactDTO contact,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<bool>> EditContactAsync(
		EditEmploymentVerificationContactDTO contact,
		CancellationToken cancellationToken = default);
}
