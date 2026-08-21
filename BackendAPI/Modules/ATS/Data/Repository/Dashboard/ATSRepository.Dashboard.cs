namespace ATS.Data.Repository;

public partial class ATSRepository
{
	public async Task<IReadOnlyList<EmailInvitationRequest>> GetDashboardDataAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var invitations = _dbcontext.EmailInvitationRequests.AsNoTracking();
		if (authorizedClientIds is not null)
		{
			invitations = invitations.Where(invitation => invitation.ClientId.HasValue
				&& authorizedClientIds.Contains(invitation.ClientId.Value));
		}
		if (requiredRequestorId.HasValue)
		{
			invitations = invitations.Where(invitation =>
				invitation.RequestorId == requiredRequestorId.Value);
		}

		return await invitations
			.Include(invitation => invitation.ReportDetails)
			.ToListAsync(cancellationToken);
	}
}
