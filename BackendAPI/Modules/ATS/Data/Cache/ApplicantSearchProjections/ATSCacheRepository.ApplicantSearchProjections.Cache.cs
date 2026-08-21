namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public async Task<List<EmailInvitationRequest>> GetEmailInvitationRequestsNeedingProjectionAsync(CancellationToken cancellationToken)
	{
		return await _atsRepository.GetEmailInvitationRequestsNeedingProjectionAsync(cancellationToken);
	}

	public async Task<ApplicantSearchProjection?> GetApplicantSearchProjectionByIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetApplicantSearchProjectionByIdAsync(emailInvitationRequestId, cancellationToken);
	}

	public async Task<bool> AddApplicantSearchProjectionAsync(ApplicantSearchProjection projection, CancellationToken cancellationToken)
	{
		return await _atsRepository.AddApplicantSearchProjectionAsync(projection, cancellationToken);
	}
}
