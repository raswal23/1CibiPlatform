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
	Task<bool> SendApplicationFormToUserEmailAsync(string gmail, string name, string applicationFormLink, string? requestor, int? clientId);

	/// <summary>
	/// As <see cref="SendApplicationFormToUserEmailAsync"/>, but reports WHY a send failed.
	///
	/// The bulk email job needs the distinction: a permanent rejection should fail the row
	/// once rather than five times, and a provider throttle should stop the whole pass.
	/// The bool overload cannot express either, so it stays for the single-order paths that
	/// genuinely only care whether the mail went out.
	/// </summary>
	Task<EmailDeliveryResult> SendApplicationFormToUserEmailWithResultAsync(
		string gmail,
		string name,
		string applicationFormLink,
		string? requestor,
		int? clientId,
		CancellationToken cancellationToken);
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
}
