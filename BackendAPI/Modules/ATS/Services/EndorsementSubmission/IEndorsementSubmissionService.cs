namespace ATS.Services.EndorsementSubmission;

public interface IEndorsementSubmissionService
{
	Task<string> GetBulkTemplateFileUrlAsync();

	// The source records how the order reached us - the web console or the public API -
	// on the order's history entry. It defaults to Web so existing callers are unchanged.
	Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default, string source = OrderHistorySource.Web);
	Task<bool> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO, CancellationToken ct = default, string source = OrderHistorySource.Web);
	Task<bool> BulkUploadFileNameExistsAsync(string fileName, CancellationToken ct = default);
	Task<IReadOnlyList<int>> GetInvalidBulkMobileNumberRowsAsync(IFormFile file, CancellationToken ct = default);
	Task<bool> SendApplicationFormToUserEmailAsync(string gmail, string name, string applicationFormLink, string? requestor, Guid? requestorId, int? clientId);

	/// <summary>
	/// As <see cref="SendApplicationFormToUserEmailAsync"/>, but reports WHY a send failed.
	///
	/// The bulk email job needs the distinction: a permanent rejection should fail the row
	/// once rather than five times, and a provider throttle should stop the whole pass.
	/// The bool overload cannot express either, so it stays for the single-order paths that
	/// genuinely only care whether the mail went out.
	/// </summary>
	/// <param name="requestor">
	/// The requestor's DISPLAY NAME, interpolated into the body's opening sentence.
	/// </param>
	/// <param name="requestorId">
	/// The requestor's user id, used only to look up the mailbox they are copied on. Separate
	/// from <paramref name="requestor"/> because the order stores the name it was raised under
	/// as text, and that text is not an address - the directory is the only source for one.
	/// Null, or an id the directory no longer resolves, simply leaves the requestor off the
	/// copy; it never fails the send.
	/// </param>
	/// <param name="isFollowUp">
	/// Sends the reminder subject and body instead of the first-invitation ones. The link is
	/// identical either way - a reminder points at the URL the candidate already has.
	/// </param>
	Task<EmailDeliveryResult> SendApplicationFormToUserEmailWithResultAsync(
		string gmail,
		string name,
		string applicationFormLink,
		string? requestor,
		Guid? requestorId,
		int? clientId,
		CancellationToken cancellationToken,
		bool isFollowUp = false);
	Task<KeysetPaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, CancellationToken cancellationToken);

	/// <summary>
	/// The set form of <see cref="ResendApplicationFormAsync"/>, for the bulk upload
	/// dialog's multi-select. Each invitation is requeued with its own fresh token and a
	/// reset attempt budget; the background job delivers them through the paced, pooled
	/// send path.
	///
	/// Ids outside the caller's scope are dropped silently, and ids the job is actively
	/// sending are skipped rather than raced - so the result reports requested versus
	/// actually requeued instead of failing a partly-stale selection.
	/// </summary>
	Task<BulkRetryResultDTO> ResendApplicationFormsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken);

	/// <summary>
	/// Queues the package follow-up reminder for every order whose interval has elapsed, and
	/// returns how many were released. Driven by the background job, not by a user action -
	/// so there is no scope check here, unlike the resend paths above.
	/// </summary>
	/// <remarks>
	/// The reminder reuses the candidate's EXISTING link, so the email they already have
	/// keeps working. Nothing is sent from here: the rows go back to Pending and the email
	/// worker delivers them, which is what keeps reminders inside the per-account daily cap
	/// and the send pacing.
	/// </remarks>
	Task<int> ReleaseDueFollowUpEmailsAsync(CancellationToken cancellationToken);
}
