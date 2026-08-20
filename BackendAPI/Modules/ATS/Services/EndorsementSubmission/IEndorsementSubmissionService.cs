namespace ATS.Services.EndorsementSubmission;

public interface IEndorsementSubmissionService
{
	Task<string> GetBulkTemplateFileUrlAsync();
	Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default);
	Task<bool> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO, CancellationToken ct = default);
	Task<bool> SendApplicationFormToUserEmailAsync(string gmail, string name, string applicationFormLink);
	Task<KeysetPaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, CancellationToken cancellationToken);
}
