namespace FrontendWebassembly.Services.ATS.Interface;

public interface IEndorsementSubmissionService
{
	Task<string> DownloadBulkTemplateAsync();
	Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO);
	Task<bool> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO);
}
