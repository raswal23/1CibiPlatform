namespace FrontendWebassembly.Services.ATS.EmailProcessManagement;

/// <summary>
/// The copy list each ATS notice sends to - who is Cc'd on a withdrawal, a dispute, an
/// application form, a follow-up or a submitted form.
/// </summary>
/// <remarks>
/// See docs/features/ats-email-process/ats-email-process.md. The read returns the whole list
/// rather than a page: there is one row per notice, so five today, and the backend caches that
/// whole-table read under a single key that every send path also reads through.
/// </remarks>
public interface IEmailProcessManagementService
{
	/// <summary>Every copy list, active and inactive, ordered by process name.</summary>
	Task<ServiceResponse<List<EmailProcessDetailsDTO>>> GetEmailProcessesAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Registers a copy list for a notice that does not have one yet.
	/// </summary>
	/// <remarks>
	/// Fails with the backend's own message when the notice already has a row, which is the
	/// ordinary case rather than an edge one: every process is seeded, so this is how a missing
	/// row is put back, not how the list is first created.
	/// </remarks>
	Task<ServiceResponse<EmailProcessDetailsDTO>> AddEmailProcessAsync(
		AddEmailProcessDTO emailProcess,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves the name, the copy list and the on/off flag. The list replaces the stored one
	/// wholesale.
	/// </summary>
	Task<ServiceResponse<EmailProcessDetailsDTO>> EditEmailProcessAsync(
		EditEmailProcessDTO emailProcess,
		CancellationToken cancellationToken = default);
}
