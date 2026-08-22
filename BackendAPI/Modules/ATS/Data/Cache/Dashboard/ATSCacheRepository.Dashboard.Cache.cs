namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public Task<IReadOnlyList<EmailInvitationRequest>> GetDashboardDataAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		DateTime windowStart,
		CancellationToken cancellationToken)
	{
		return _atsRepository.GetDashboardDataAsync(
			authorizedClientIds,
			requiredRequestorId,
			windowStart,
			cancellationToken);
	}
}
