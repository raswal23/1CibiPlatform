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
}
