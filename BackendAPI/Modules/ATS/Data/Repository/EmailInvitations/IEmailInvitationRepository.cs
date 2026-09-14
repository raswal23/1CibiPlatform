namespace ATS.Data.Repository;

public interface IEmailInvitationRepository
{
	Task<bool> AddEmailInvitationRequestAsync(EmailInvitationRequest emailInvitationRequest);
	Task<bool> AddBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync();
	Task<int> ReleaseStaleEmailInvitationClaimsAsync(TimeSpan staleAfter);

	/// <summary>
	/// Returns claimed rows to Pending WITHOUT charging them a send attempt, for work the
	/// pass abandoned before offering it to the SMTP server (a provider throttle).
	/// </summary>
	Task<int> ReleaseEmailInvitationClaimsAsync(List<EmailInvitationRequest> emailInvitationRequests);

	/// <summary>
	/// Puts one invitation back on the email queue with a fresh token and a reset attempt
	/// budget, for an operator-forced retry. Returns false when the row is not in a
	/// retryable state, so a stale button reports a conflict instead of a silent no-op.
	///
	/// The counterpart to <c>RequeueExhaustedTicketAsync</c> on the ticketing side, and
	/// deliberately the same shape: the status predicate lives inside the UPDATE so it
	/// doubles as the concurrency guard.
	/// </summary>
	Task<bool> RequeueEmailInvitationAsync(
		Guid emailInvitationId,
		string hashToken,
		DateTime hashTokenExpiration,
		CancellationToken cancellationToken);

	/// <summary>
	/// The set form of <see cref="RequeueEmailInvitationAsync"/>. Each entry carries its own
	/// token, because a shared one would let any candidate in the batch open another
	/// candidate's form. Returns how many rows actually moved.
	/// </summary>
	Task<int> RequeueEmailInvitationsAsync(
		IReadOnlyCollection<EmailInvitationRequeueDTO> requeues,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads the scope identity of many invitations at once, so a bulk resend can enforce
	/// the caller's scope per row. Ids that do not exist are absent from the result.
	/// </summary>
	Task<List<EmailInvitationOwnerDTO>> GetEmailInvitationOwnersAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken);
	Task<bool> UpdateBulkEmailInvitationRequestForSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateBulkEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid emailInvitationId);
	Task<bool> UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(Guid emailInvitationId);
	Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken);
	Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken);
	/// <summary>
	/// Reads one order's identity for an access check, without loading the whole row.
	/// Returns null when the order does not exist.
	/// </summary>
	Task<EmailInvitationOwnerDTO?> GetEmailInvitationOwnerAsync(Guid emailInvitationId, CancellationToken cancellationToken);
	Task<bool> UpdateSubjectNameAsync(EditSubjectNameDTO subjectName, CancellationToken cancellationToken);
}
