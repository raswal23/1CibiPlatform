namespace ATS.Services;

public interface IInsertEmailInvitationRequestService
{
	Task<Guid> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default);

}
