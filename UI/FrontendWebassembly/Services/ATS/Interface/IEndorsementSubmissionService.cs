namespace FrontendWebassembly.Services.ATS.Interface;

public interface IEndorsementSubmissionService
{
	Task<string> DownloadBulkTemplateAsync();
	Task<bool> InsertEmailInvitationRequest(EmailInvitationRequestDTO emailInvitationRequestDTO);
}
