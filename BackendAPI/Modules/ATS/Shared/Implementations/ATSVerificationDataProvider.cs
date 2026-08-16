namespace ATS.Shared.Implementations;

public sealed class ATSVerificationDataProvider(ATSDBContext db) : IATSVerificationDataProvider
{
	public async Task<IReadOnlyList<ATSInProgressEmploymentRecord>> GetInProgressEmploymentAsync(CancellationToken cancellationToken = default)
	{
		return await (
			from user in db.UserDetails.AsNoTracking()
			join invitation in db.EmailInvitationRequests.AsNoTracking()
				on user.UserId equals invitation.RequestorId
			join employment in db.ProfessionalExperiences.AsNoTracking()
				on invitation.EmailInvitationID equals employment.EmailInvitationID
			where invitation.OrderStatus == "In Progress"
			select new ATSInProgressEmploymentRecord(
				invitation.EmailInvitationID,
				((invitation.FirstName ?? string.Empty) + " "
					+ (invitation.MiddleInitial ?? string.Empty) + " "
					+ (invitation.LastName ?? string.Empty)).Trim(),
				employment.Emp1CompanyName ?? string.Empty,
				employment.Emp1JobTitle,
				employment.Emp1StartDate,
				employment.Emp1EndDate,
				employment.Emp1SupervisorName,
				user.UserEmail))
			.Distinct()
			.ToListAsync(cancellationToken);
	}
}
