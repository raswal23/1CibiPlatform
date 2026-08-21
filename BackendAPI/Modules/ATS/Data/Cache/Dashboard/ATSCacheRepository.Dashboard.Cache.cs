namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public Task<IReadOnlyList<EmailInvitationRequest>> GetDashboardDataAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		return _atsRepository.GetDashboardDataAsync(
			authorizedClientIds,
			requiredRequestorId,
			cancellationToken);
	}
}
