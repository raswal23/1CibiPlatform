namespace FrontendWebassembly.Services.ATS.EndorsementSubmission;

public interface IEndorsementSubmissionService : IAsyncDisposable
{
	event Action<string> ATSResponseReceived;

	Task StartAsync();
	Task<ServiceResponse<string>> DownloadBulkTemplateAsync();
	Task<ServiceResponse<bool>> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO);
	Task<ServiceResponse<bool>> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO);
	Task<ServiceResponse<KeysetPaginatedResult<EmailInvitationRequestListDTO>>> GetWithdrawnEmailInvitationRequestsAsync(string? cursor = null, int? pageSize = 10, string? SearchTerm = null);
	Task<ServiceResponse<bool>> ResendApplicationFormAsync(Guid emailInvitationId);

	/// <summary>
	/// Resends many invitations at once. The result carries requested versus actually
	/// requeued, because a stale selection is skipped rather than failed.
	/// </summary>
	Task<ServiceResponse<BulkRetryResultDTO>> ResendApplicationFormsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds);
}
