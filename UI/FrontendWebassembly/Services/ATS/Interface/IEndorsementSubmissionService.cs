namespace FrontendWebassembly.Services.ATS.Interface;

public interface IEndorsementSubmissionService
{
	event Action<string> ATSResponseReceived;

	Task StartAsync();
	Task<string> DownloadBulkTemplateAsync();
	Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO);
	Task<bool> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO);
}
