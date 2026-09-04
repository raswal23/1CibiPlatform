namespace ATS.Services.EndorsementSubmission;

public interface IEndorsementSubmissionService
{
	Task<string> GetBulkTemplateFileUrlAsync();

	// The source records how the order reached us - the web console or the public API -
	// on the order's history entry. It defaults to Web so existing callers are unchanged.
	Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default, string source = OrderHistorySource.Web);
	Task<bool> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO, CancellationToken ct = default, string source = OrderHistorySource.Web);
	Task<bool> SendApplicationFormToUserEmailAsync(string gmail, string name, string applicationFormLink, string? requestor, int? clientId);
	Task<KeysetPaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, CancellationToken cancellationToken);
}
