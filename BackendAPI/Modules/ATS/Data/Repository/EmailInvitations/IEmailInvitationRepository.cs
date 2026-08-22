namespace ATS.Data.Repository;

public interface IEmailInvitationRepository
{
	Task<bool> AddEmailInvitationRequestAsync(EmailInvitationRequest emailInvitationRequest);
	Task<bool> AddBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync();
	Task<int> ReleaseStaleEmailInvitationClaimsAsync(TimeSpan staleAfter);
	Task<bool> UpdateBulkEmailInvitationRequestForSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateBulkEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid emailInvitationId);
	Task<bool> UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(Guid emailInvitationId);
	Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken);
	Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken);
}
