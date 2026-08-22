namespace ATS.Data.Repository;

public interface IDashboardRepository
{
	Task<IReadOnlyList<EmailInvitationRequest>> GetDashboardDataAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		DateTime windowStart,
		CancellationToken cancellationToken);
}
