namespace ATS.Services;

public interface IInsertEmailInvitationRequestService
{
	Task<Guid> InsertEmailInvitationRequest(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default);

}
