namespace ATS.Data.Repository;

/// <summary>
/// Persistence for the per-notice copy lists in <c>ats."EmailProcessDetails"</c>.
/// </summary>
public interface IEmailProcessRepository
{
	/// <summary>
	/// Every copy list, active and inactive, ordered by process name.
	/// </summary>
	/// <remarks>
	/// Unpaged, unlike the package and client lists: the table has one row per value in
	/// <see cref="AtsEmailProcess.All"/>, so it is five rows today and will not grow without a
	/// code change adding the constant. A cursor here would be scaffolding around a list that
	/// fits on one screen.
	///
	/// This is also what the send path reads, through
	/// <c>IEmailProcessManagementService.GetCopyListAsync</c> - the whole table is one cached entry,
	/// so resolving one notice's list costs no query of its own.
	/// </remarks>
	Task<List<EmailProcessDetailsDTO>> GetEmailProcessesAsync(CancellationToken cancellationToken);

	/// <summary>The row itself, tracked, for an edit to mutate.</summary>
	Task<EmailProcessDetails?> GetEmailProcessAsync(int id, CancellationToken cancellationToken);

	/// <summary>
	/// True when a row already exists for this process, matched case-insensitively.
	/// </summary>
	/// <remarks>
	/// The unique index would reject the duplicate anyway, but as a <c>DbUpdateException</c>
	/// that reaches the caller as a 500. This turns it into the 400 the screen can render.
	/// Case-insensitive because the index is not: "withdrawn" and "Withdrawn" are two rows to
	/// PostgreSQL and one notice to the send path, so the index alone would let the pair
	/// through. The validator closes the value to <see cref="AtsEmailProcess.All"/> before this
	/// runs, which makes a differing case the only way to reach it.
	/// </remarks>
	Task<bool> EmailProcessExistsAsync(string emailProcess, CancellationToken cancellationToken);

	Task<EmailProcessDetails> AddEmailProcessAsync(
		EmailProcessDetails emailProcess,
		CancellationToken cancellationToken);

	Task<EmailProcessDetails> EditEmailProcessAsync(
		EmailProcessDetails emailProcess,
		CancellationToken cancellationToken);
}
