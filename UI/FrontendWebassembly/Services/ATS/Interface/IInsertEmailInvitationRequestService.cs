namespace FrontendWebassembly.Services.ATS.Interface;

public interface IInsertEmailInvitationRequestService
{
	Task<Guid> InsertEmailInvitationRequest(EmailInvitationRequestDTO emailInvitationRequestDTO);
}
