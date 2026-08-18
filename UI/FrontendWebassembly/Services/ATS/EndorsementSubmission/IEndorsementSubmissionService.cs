namespace FrontendWebassembly.Services.ATS.EndorsementSubmission;

public interface IEndorsementSubmissionService
{
	event Action<string> ATSResponseReceived;

	Task StartAsync();
	Task<ServiceResponse<string>> DownloadBulkTemplateAsync();
	Task<ServiceResponse<bool>> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO);
	Task<ServiceResponse<bool>> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO);
	Task<ServiceResponse<PaginatedResult<EmailInvitationRequestListDTO>>> GetWithdrawnEmailInvitationRequestsAsync(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null);
	Task<ServiceResponse<bool>> ResendApplicationFormAsync(Guid emailInvitationId);
}
