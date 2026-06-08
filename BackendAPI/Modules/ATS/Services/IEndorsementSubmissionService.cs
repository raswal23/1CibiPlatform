namespace ATS.Services;

public interface IEndorsementSubmissionService
{
	Task<string> GetBulkTemplateFileUrlAsync();
	Task<bool> InsertEmailInvitationRequest(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default);

}
