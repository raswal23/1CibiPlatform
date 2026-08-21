namespace ATS.Data.Repository;

public partial class ATSRepository
{
	public async Task<List<EmailInvitationRequest>> GetEmailInvitationRequestsNeedingProjectionAsync(CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
			.Where(x => x.NeedsProjection)
			.Include(x => x.PersonalDetails)
			.Include(x => x.AddressDetails)
			.Include(x => x.EducationalBackground)
			.Include(x => x.LicensesDetails)
			.Include(x => x.ProfessionalExperiences)
			.Include(x => x.ReferenceDetails)
			.Include(x => x.SignatureDetails)
			.ToListAsync(cancellationToken);
	}

	public async Task<ApplicantSearchProjection?> GetApplicantSearchProjectionByIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		return await _dbcontext.ApplicantSearchProjections
			.FirstOrDefaultAsync(x => x.EmailInvitationRequestId == emailInvitationRequestId, cancellationToken);
	}

	public async Task<bool> AddApplicantSearchProjectionAsync(ApplicantSearchProjection projection, CancellationToken cancellationToken)
	{
		await _dbcontext.ApplicantSearchProjections.AddAsync(projection, cancellationToken);
		return true;
	}
}
