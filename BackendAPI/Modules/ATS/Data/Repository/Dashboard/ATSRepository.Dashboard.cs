namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Bounded by date. DashboardService discards everything outside the current year
	// (YTD series) and the trailing turnaround window anyway, so pulling the whole
	// table - which for a platform super admin was every invitation plus every related
	// ReportDetails row, on every dashboard load - bought nothing.
	public async Task<IReadOnlyList<EmailInvitationRequest>> GetDashboardDataAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		DateTime windowStart,
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

		// Keep rows with no OrderCreatedAt: the candidate-response tiles count
		// invitations by EmailSentStatus, which does not depend on the order date.
		invitations = invitations.Where(invitation =>
			!invitation.OrderCreatedAt.HasValue
			|| invitation.OrderCreatedAt.Value >= windowStart);

		return await invitations
			.Include(invitation => invitation.ReportDetails)
			.ToListAsync(cancellationToken);
	}
}
